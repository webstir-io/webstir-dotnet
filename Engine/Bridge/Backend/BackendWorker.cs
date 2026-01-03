using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Engine.Bridge;
using Engine.Bridge.Module;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Engine.Workflows;
using Framework.Packaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Engine.Bridge.Backend;

public class BackendWorker(AppWorkspace workspace, IOptions<AppSettings> options, ILogger<BackendWorker> logger) : IWorkflowWorker
{
    private readonly AppSettings _settings = options.Value;
    private const string _tsConfigFile = "tsconfig.json";
    private readonly IBackendModuleProviderResolver _moduleProviderResolver = new DefaultBackendModuleProviderResolver();
    private BackendModuleProvider? _resolvedProvider;
    private readonly ILogger<BackendWorker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SemaphoreSlim _packageLock = new(1, 1);
    private bool _packagesVerified;
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public int BuildOrder => 2;

    public Task InitAsync(WorkspaceProfile profile)
    {
        if (!profile.HasBackend)
        {
            return Task.CompletedTask;
        }

        if (Directory.Exists(workspace.BackendPath) && Directory.GetFileSystemEntries(workspace.BackendPath).Length > 0)
        {
            return Task.CompletedTask;
        }

        throw new WorkflowUsageException(
            $"Backend scaffold is missing at '{workspace.BackendPath}'. " +
            $"Run '{App.Name} {Commands.Repair} {RepairOptions.DryRun}' to see what will be restored, then '{App.Name} {Commands.Repair}'.");
    }

    public async Task BuildAsync(string? changedFilePath = null)
    {
        if (!string.IsNullOrEmpty(changedFilePath) && !BuildHelpers.ContainsBuildFolder(changedFilePath, Folders.Backend))
        {
            return;
        }

        await EnsurePackagesAsync();

        BackendModuleProvider provider = await EnsureProviderAsync();

        Dictionary<string, string?> env = new(StringComparer.Ordinal)
        {
            ["API_PORT"] = _settings.ApiServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["WEB_PORT"] = _settings.WebServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        bool incremental = !string.IsNullOrEmpty(changedFilePath);

        ModuleBuildExecutionResult result = await ModuleBuildExecutor.ExecuteAsync(
            workspace,
            provider.Id,
            ModuleBuildMode.Build,
            env,
            incremental,
            _logger,
            CancellationToken.None);

        LogBackendManifest("Build", result);
    }

    public async Task PublishAsync()
    {
        await EnsurePackagesAsync();

        BackendModuleProvider provider = await EnsureProviderAsync();
        Dictionary<string, string?> env = new(StringComparer.Ordinal)
        {
            ["API_PORT"] = _settings.ApiServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["WEB_PORT"] = _settings.WebServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        bool emitSourceMaps = ShouldEmitSourceMaps();
        if (emitSourceMaps)
        {
            env["WEBSTIR_BACKEND_SOURCEMAPS"] = "on";
        }

        ModuleBuildExecutionResult result = await ModuleBuildExecutor.ExecuteAsync(
            workspace,
            provider.Id,
            ModuleBuildMode.Publish,
            env,
            incremental: false,
            _logger,
            CancellationToken.None);

        foreach (string jsFilepath in Directory.GetFiles(workspace.BackendBuildPath, "*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(workspace.BackendBuildPath, jsFilepath);
            string targetFilePath = Path.Combine(workspace.BackendDistPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);

            string jsContent = File.ReadAllText(jsFilepath);
            if (!emitSourceMaps)
            {
                jsContent = RemoveJavaScriptComments(jsContent);
            }
            else if (!jsContent.Contains("sourceMappingURL=", StringComparison.Ordinal))
            {
                string fileName = Path.GetFileName(targetFilePath);
                jsContent += $"\n//# sourceMappingURL={fileName}{FileExtensions.Map}";
            }

            File.WriteAllText(targetFilePath, jsContent);
        }

        if (emitSourceMaps)
        {
            foreach (string mapPath in Directory.GetFiles(workspace.BackendBuildPath, "*.map", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(workspace.BackendBuildPath, mapPath);
                string target = Path.Combine(workspace.BackendDistPath, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(mapPath, target, overwrite: true);
            }
        }

        LogBackendManifest("Publish", result);
    }

    private static string RemoveJavaScriptComments(string js)
    {
        string singleLinePattern = @"(?<!:)//.*$";
        js = System.Text.RegularExpressions.Regex.Replace(
            js,
            singleLinePattern,
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        string multiLinePattern = @"/\*[\s\S]*?\*/";
        js = System.Text.RegularExpressions.Regex.Replace(js, multiLinePattern, string.Empty);

        string emptyLinePattern = @"^\s*\r?\n";
        js = System.Text.RegularExpressions.Regex.Replace(
            js,
            emptyLinePattern,
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        return js.Trim();
    }

    private static bool ShouldEmitSourceMaps()
    {
        string? flag = Environment.GetEnvironmentVariable("WEBSTIR_BACKEND_SOURCEMAPS");
        if (string.IsNullOrWhiteSpace(flag))
        {
            return false;
        }

        string v = flag.Trim().ToLowerInvariant();
        return v is "on" or "true" or "1";
    }

    private async Task EnsurePackagesAsync()
    {
        await _packageLock.WaitAsync();
        try
        {
            if (_packagesVerified)
            {
                return;
            }

            NodeRuntime.EnsureMinimumVersion();

            BackendModuleProvider backendProvider = await EnsureProviderAsync();
            string backendProviderId = backendProvider.Id;
            string? backendProviderSpec = ProviderSpecOverrides.GetBackendProviderSpec();
            string? backendDependencyOverride = ProviderSpecOverrides.GetDefaultProviderSpec(
                backendProviderId,
                ProviderSpecOverrides.DefaultBackendProviderId,
                backendProviderSpec);

            PackageWorkspaceAdapter workspaceAdapter = new(workspace);
            PackageManagerDescriptor packageManager = workspaceAdapter.PackageManager;
            PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
                workspaceAdapter,
                _logger,
                ensureFrontend: null,
                ensureTesting: null,
                ensureBackend: () => BackendPackageInstaller.EnsureAsync(workspaceAdapter, backendDependencyOverride),
                includeFrontend: false,
                includeTesting: false,
                includeBackend: true,
                autoInstall: true);

            if (summary.InstallPerformed)
            {
                _logger.LogInformation("[backend] Package dependencies refreshed; {Manager} install completed.", packageManager.Executable);
            }
            else
            {
                _logger.LogDebug("[backend] Packages already up to date.");
            }

            if (summary.Backend is { DependencyUpdated: true } backend)
            {
                _logger.LogInformation("[backend] {Package} dependency updated to match bundled version.", backend.Metadata.Name);
            }

            if (summary.InstallRequiredButSkipped)
            {
                throw new InvalidOperationException($"Framework packages require installation. Run '{App.Name} install' to synchronize dependencies.");
            }

            if (summary.HasVersionMismatch)
            {
                ThrowMismatch(summary);
            }

            await ProviderPackageInstaller.EnsureAsync(
                workspaceAdapter,
                backendProviderId,
                backendProviderSpec,
                ProviderSpecOverrides.DefaultBackendProviderId,
                "backend",
                message => _logger.LogInformation(message)).ConfigureAwait(false);

            _packagesVerified = true;
        }
        finally
        {
            _packageLock.Release();
        }
    }

    private void ThrowMismatch(PackageEnsureSummary summary)
    {
        if (summary.Backend is not { VersionMismatch: true } backend)
        {
            return;
        }

        string installed = string.IsNullOrWhiteSpace(backend.InstalledVersion)
            ? "missing"
            : backend.InstalledVersion!;
        _logger.LogWarning(
            "{Package} {InstalledVersion} detected but {ExpectedVersion} is bundled. Run '{Command} install' to refresh dependencies.",
            backend.Metadata.Name,
            installed,
            backend.Metadata.Version,
            App.Name);
        throw new InvalidOperationException(
            $"Framework packages are out of sync: {backend.Metadata.Name} (found {installed}, expected {backend.Metadata.Version}). Run '{App.Name} install' to synchronize dependencies.");
    }

    private async Task<BackendModuleProvider> EnsureProviderAsync()
    {
        if (_resolvedProvider is not null)
        {
            return _resolvedProvider;
        }

        _resolvedProvider = await _moduleProviderResolver.ResolveAsync(workspace, CancellationToken.None);
        return _resolvedProvider;
    }

    private void LogBackendManifest(string stage, ModuleBuildExecutionResult result)
    {
        _logger.LogDebug(
            "[backend] {Stage} provider {ProviderId} produced {EntryCount} entry point(s).",
            stage,
            result.Provider.Id,
            result.Manifest.EntryPoints.Count);

        if (result.Manifest.Module is { Routes: { } routes })
        {
            _logger.LogDebug(
                "[backend] {Stage} manifest includes {RouteCount} route(s) and {CapabilityCount} capability flag(s).",
                stage,
                routes.Count,
                result.Manifest.Module.Capabilities?.Count ?? 0);
        }
        else
        {
            _logger.LogDebug("[backend] {Stage} manifest did not include route definitions.", stage);
        }

        PersistBackendManifest(result.Manifest);

        foreach (ModuleDiagnostic diagnostic in result.Manifest.Diagnostics)
        {
            if (string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[backend] {Message}", diagnostic.Message);
            }
            else if (string.Equals(diagnostic.Severity, "warn", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[backend] {Message}", diagnostic.Message);
            }
            else
            {
                _logger.LogDebug("[backend] {Message}", diagnostic.Message);
            }
        }

        foreach (ModuleLogEvent evt in result.Events)
        {
            LogModuleEvent(evt);
        }
    }

    private void PersistBackendManifest(ModuleBuildManifest manifest)
    {
        try
        {
            Directory.CreateDirectory(workspace.WebstirPath);
            string manifestPath = workspace.BackendManifestPath;
            string payload = JsonSerializer.Serialize(manifest, ManifestSerializerOptions);
            File.WriteAllText(manifestPath, payload);
            _logger.LogDebug("[backend] backend manifest written to {ManifestPath}.", manifestPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[backend] failed to persist backend manifest.");
        }
    }

    private void LogModuleEvent(ModuleLogEvent evt)
    {
        if (string.Equals(evt.Type, "error", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("[backend] {Message}", evt.Message);
        }
        else if (string.Equals(evt.Type, "warn", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[backend] {Message}", evt.Message);
        }
        else
        {
            _logger.LogDebug("[backend] {Message}", evt.Message);
        }
    }

}
