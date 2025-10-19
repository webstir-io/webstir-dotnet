using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Engine.Bridge;
using Engine.Bridge.Module;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
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

    public int BuildOrder => 2;

    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.BackendPath, workspace.BackendPath);

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
            jsContent = RemoveJavaScriptComments(jsContent);

            File.WriteAllText(targetFilePath, jsContent);
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

            PackageWorkspaceAdapter workspaceAdapter = new(workspace);
            PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
                workspaceAdapter,
                _logger,
                ensureFrontend: null,
                ensureTesting: null,
                ensureBackend: () => BackendPackageInstaller.EnsureAsync(workspaceAdapter),
                includeFrontend: false,
                includeTesting: false,
                includeBackend: true,
                autoInstall: true);

            if (summary.InstallPerformed)
            {
                _logger.LogInformation("[backend] Package dependencies refreshed; npm install completed.");
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
        _logger.LogInformation(
            "[backend] {Stage} provider {ProviderId} produced {EntryCount} entry point(s).",
            stage,
            result.Provider.Id,
            result.Manifest.EntryPoints.Count);

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
                _logger.LogInformation("[backend] {Message}", diagnostic.Message);
            }
        }

        foreach (ModuleLogEvent evt in result.Events)
        {
            LogModuleEvent(evt);
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
            _logger.LogInformation("[backend] {Message}", evt.Message);
        }
    }

}
