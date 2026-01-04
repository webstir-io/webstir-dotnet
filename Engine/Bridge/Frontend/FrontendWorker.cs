using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Engine.Bridge.Module;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Engine.Workflows;
using Framework.Packaging;
using Microsoft.Extensions.Logging;

namespace Engine.Bridge.Frontend;

public sealed class FrontendWorker : IFrontendWorker
{
    private readonly AppWorkspace _workspace;
    private readonly ILogger<FrontendWorker> _logger;

    private const string DiagnosticPrefix = "WEBSTIR_DIAGNOSTIC ";
    private static readonly JsonSerializerOptions DiagnosticSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly FrontendWatcher _watcher;
    private readonly ConcurrentQueue<FrontendHotUpdate> _hotUpdates = new();
    private bool _watchModeEnabled;
    private readonly SemaphoreSlim _packageLock = new(1, 1);
    private bool _packagesVerified;
    private bool _buildSummaryLogged;
    private readonly IFrontendModuleProviderResolver _moduleProviderResolver;
    private FrontendModuleProvider? _resolvedProvider;

    public FrontendWorker(AppWorkspace workspace, ILogger<FrontendWorker> logger)
    {
        _workspace = workspace;
        _logger = logger;
        bool verboseLogging = IsVerboseWatchLoggingEnabled();
        bool hmrVerboseLogging = IsHmrVerboseLoggingEnabled();
        _moduleProviderResolver = new DefaultFrontendModuleProviderResolver();
        _watcher = new FrontendWatcher(
            _workspace,
            _logger,
            DiagnosticPrefix,
            DiagnosticSerializerOptions,
            diagnostic => HandleWatchDiagnostic(diagnostic),
            (line, isError) => HandleWatchOutput(line, isError),
            GetExecutablePath,
            verboseLogging: verboseLogging,
            hotUpdateHandler: hotUpdate => _hotUpdates.Enqueue(hotUpdate),
            hmrVerboseLogging: hmrVerboseLogging);

        if (verboseLogging)
        {
            _logger.LogInformation("[frontend] Verbose frontend watch logging enabled (WEBSTIR_FRONTEND_WATCH_VERBOSE).");
        }
        if (hmrVerboseLogging)
        {
            _logger.LogInformation("[frontend] HMR verbose logging enabled (WEBSTIR_FRONTEND_HMR_VERBOSE).");
        }
    }

    public int BuildOrder => 1;

    public Task InitAsync(WorkspaceProfile profile)
    {
        if (!profile.HasFrontend)
        {
            return Task.CompletedTask;
        }

        // If a mode-specific template already populated the frontend folder, avoid overwriting it.
        if (Directory.Exists(_workspace.FrontendPath) && Directory.GetFileSystemEntries(_workspace.FrontendPath).Length > 0)
        {
            return Task.CompletedTask;
        }

        throw new WorkflowUsageException(
            $"Frontend scaffold is missing at '{_workspace.FrontendPath}'. " +
            $"Run '{App.Name} {Commands.Repair} {RepairOptions.DryRun}' to see what will be restored, then '{App.Name} {Commands.Repair}'.");
    }

    public async Task BuildAsync(string? changedFilePath = null)
    {
        if (_watchModeEnabled)
        {
            object payload = string.IsNullOrWhiteSpace(changedFilePath)
                ? new
                {
                    type = "reload"
                }
                : new
                {
                    type = "change",
                    path = changedFilePath
                };
            ResetHotUpdateQueue();
            await _watcher.SendAsync(payload, waitForCompletion: true, CancellationToken.None);
            return;
        }

        await EnsurePackagesAsync();
        FrontendModuleProvider provider = await EnsureProviderAsync();

        ModuleBuildExecutionResult buildResult = await ModuleBuildExecutor.ExecuteAsync(
            _workspace,
            provider.Id,
            ModuleBuildMode.Build,
            new Dictionary<string, string?>(StringComparer.Ordinal),
            incremental: false,
            _logger,
            CancellationToken.None);

        LogModuleBuildResult("Build", buildResult);
    }

    public async Task PublishAsync()
    {
        await EnsurePackagesAsync();
        FrontendModuleProvider provider = await EnsureProviderAsync();

        Dictionary<string, string?> env = new(StringComparer.Ordinal);
        string? frontendMode = Environment.GetEnvironmentVariable("WEBSTIR_FRONTEND_MODE");
        if (!string.IsNullOrWhiteSpace(frontendMode))
        {
            env["WEBSTIR_FRONTEND_MODE"] = frontendMode;
        }

        ModuleBuildExecutionResult buildResult = await ModuleBuildExecutor.ExecuteAsync(
            _workspace,
            provider.Id,
            ModuleBuildMode.Publish,
            env,
            incremental: false,
            _logger,
            CancellationToken.None);

        LogModuleBuildResult("Publish", buildResult);
        if (string.Equals(frontendMode, "ssg", StringComparison.OrdinalIgnoreCase))
        {
            ApplySsgPublishAliases();
        }
        await LogPublishManifestAsync();
        RemoveEmptyLegacyPagesFolder();
    }

    private void ApplySsgPublishAliases()
    {
        try
        {
            string distPagesRoot = Path.Combine(_workspace.FrontendDistPath, Folders.Pages);
            if (!Directory.Exists(distPagesRoot))
            {
                return;
            }

            Dictionary<string, string> pageIndexMap = new(StringComparer.OrdinalIgnoreCase);
            foreach (string pageDir in Directory.GetDirectories(distPagesRoot, "*", SearchOption.TopDirectoryOnly))
            {
                string pageName = Path.GetFileName(pageDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(pageName))
                {
                    continue;
                }

                string sourceIndex = Path.Combine(pageDir, Files.IndexHtml);
                if (!File.Exists(sourceIndex))
                {
                    continue;
                }

                pageIndexMap[pageName] = sourceIndex;

                string targetDir = Path.Combine(_workspace.FrontendDistPath, pageName);
                Directory.CreateDirectory(targetDir);
                string targetIndex = Path.Combine(targetDir, Files.IndexHtml);
                File.Copy(sourceIndex, targetIndex, overwrite: true);
            }

            string homeIndexPath = Path.Combine(distPagesRoot, Folders.Home, Files.IndexHtml);
            if (File.Exists(homeIndexPath))
            {
                string rootIndexPath = Path.Combine(_workspace.FrontendDistPath, Files.IndexHtml);
                Directory.CreateDirectory(_workspace.FrontendDistPath);
                File.Copy(homeIndexPath, rootIndexPath, overwrite: true);
            }

            ApplyStaticPathAliases(distPagesRoot, pageIndexMap);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "[frontend] Failed to apply SSG publish aliases.");
        }
    }

    private void RemoveEmptyLegacyPagesFolder()
    {
        try
        {
            string legacyPagesRoot = Path.Combine(_workspace.FrontendDistPath, Folders.Pages);
            if (!Directory.Exists(legacyPagesRoot))
            {
                return;
            }

            if (Directory.GetFileSystemEntries(legacyPagesRoot).Length > 0)
            {
                return;
            }

            Directory.Delete(legacyPagesRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "[frontend] Failed to remove legacy pages folder.");
        }
    }

    private void ApplyStaticPathAliases(string distPagesRoot, IReadOnlyDictionary<string, string> pageIndexMap)
    {
        if (pageIndexMap.Count == 0)
        {
            return;
        }

        string packageJsonPath = Path.Combine(_workspace.WorkingPath, Files.PackageJson);
        if (!File.Exists(packageJsonPath))
        {
            return;
        }

        using FileStream stream = File.OpenRead(packageJsonPath);
        using JsonDocument doc = JsonDocument.Parse(stream);
        if (!doc.RootElement.TryGetProperty("webstir", out JsonElement webstir) ||
            !webstir.TryGetProperty("moduleManifest", out JsonElement moduleManifestElement) ||
            !moduleManifestElement.TryGetProperty("views", out JsonElement viewsElement) ||
            viewsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement view in viewsElement.EnumerateArray())
        {
            if (view.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!IsSsgView(view))
            {
                continue;
            }

            List<string> staticPaths = ResolveStaticPaths(view);
            if (staticPaths.Count == 0)
            {
                continue;
            }

            foreach (string raw in staticPaths)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string normalized = NormalizeStaticPath(raw);
                string? sourceIndex = ResolveSourceIndexForStaticPath(distPagesRoot, normalized, pageIndexMap);
                if (string.IsNullOrEmpty(sourceIndex))
                {
                    continue;
                }

                string targetIndex = normalized == "/"
                    ? Path.Combine(_workspace.FrontendDistPath, Files.IndexHtml)
                    : Path.Combine(_workspace.FrontendDistPath, normalized.TrimStart('/'), Files.IndexHtml);

                Directory.CreateDirectory(Path.GetDirectoryName(targetIndex)!);
                File.Copy(sourceIndex, targetIndex, overwrite: true);
            }
        }
    }

    private static bool IsSsgView(JsonElement view)
    {
        if (!view.TryGetProperty("renderMode", out JsonElement renderModeElement))
        {
            return true;
        }

        string renderMode = renderModeElement.GetString() ?? string.Empty;
        return string.Equals(renderMode, "ssg", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ResolveStaticPaths(JsonElement view)
    {
        if (view.TryGetProperty("staticPaths", out JsonElement staticPathsElement) && staticPathsElement.ValueKind == JsonValueKind.Array)
        {
            List<string> paths = new();
            foreach (JsonElement pathElement in staticPathsElement.EnumerateArray())
            {
                string? raw = pathElement.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    paths.Add(raw);
                }
            }

            if (paths.Count > 0)
            {
                return paths;
            }
        }

        if (!view.TryGetProperty("path", out JsonElement pathElementValue))
        {
            return [];
        }

        string? template = pathElementValue.GetString();
        if (!IsDefaultStaticPathCandidate(template))
        {
            return [];
        }

        return [template!];
    }

    private static bool IsDefaultStaticPathCandidate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        string trimmed = template.Trim();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        return !trimmed.Contains(':', StringComparison.Ordinal) && !trimmed.Contains('*', StringComparison.Ordinal);
    }

    private static string NormalizeStaticPath(string value)
    {
        string trimmed = value.Trim();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            trimmed = "/" + trimmed;
        }

        if (trimmed.Length > 1 && trimmed.EndsWith("/", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed;
    }

    private static string? ResolveSourceIndexForStaticPath(
        string distPagesRoot,
        string normalizedPath,
        IReadOnlyDictionary<string, string> pageIndexMap)
    {
        if (normalizedPath == "/")
        {
            return pageIndexMap.TryGetValue(Folders.Home, out string? homeIndex) ? homeIndex : null;
        }

        string relativePath = normalizedPath.TrimStart('/');
        string candidate = Path.Combine(distPagesRoot, relativePath, Files.IndexHtml);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        string? pageName = FirstPathSegment(normalizedPath);
        if (string.IsNullOrWhiteSpace(pageName))
        {
            return null;
        }

        return pageIndexMap.TryGetValue(pageName, out string? pageIndex) ? pageIndex : null;
    }

    private static string? FirstPathSegment(string pathname)
    {
        string trimmed = pathname.Trim('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        int separatorIndex = trimmed.IndexOf('/', StringComparison.Ordinal);
        return separatorIndex < 0 ? trimmed : trimmed[..separatorIndex];
    }

    public async Task AddPageAsync(string pageName)
    {
        await EnsurePackagesAsync();
        await EnsureProviderAsync();
        await RunFrontendCliAsync("add-page", null, pageName);

        WorkspaceProfile profile = _workspace.DetectWorkspaceProfile();
        if (profile.Mode == WorkspaceMode.Ssg)
        {
            NormalizeSsgPageScaffold(pageName);
        }
    }

    private void NormalizeSsgPageScaffold(string pageName)
    {
        string pageRoot = Path.Combine(_workspace.FrontendPagesPath, pageName);
        string htmlPath = Path.Combine(pageRoot, $"{Files.Index}{FileExtensions.Html}");
        string tsPath = Path.Combine(pageRoot, $"{Files.Index}{FileExtensions.Ts}");

        try
        {
            if (File.Exists(tsPath))
            {
                File.Delete(tsPath);
            }

            if (!File.Exists(htmlPath))
            {
                return;
            }

            string[] lines = File.ReadAllLines(htmlPath);
            bool modified = false;
            List<string> updated = new(lines.Length);

            foreach (string line in lines)
            {
                if (line.Contains("<script", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains($"{Files.Index}{FileExtensions.Js}", StringComparison.OrdinalIgnoreCase))
                {
                    string indent = GetLeadingWhitespace(line);
                    updated.Add($"{indent}<!-- Add {Files.Index}{FileExtensions.Ts} to enable JS on this page. -->");
                    modified = true;
                    continue;
                }

                updated.Add(line);
            }

            if (modified)
            {
                File.WriteAllLines(htmlPath, updated);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "[frontend] Unable to normalize SSG scaffold for page '{PageName}'.", pageName);
        }
    }

    private static string GetLeadingWhitespace(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        int length = 0;
        while (length < value.Length && char.IsWhiteSpace(value[length]))
        {
            length += 1;
        }

        return length == 0 ? string.Empty : value[..length];
    }

    public async Task StartWatchAsync()
    {
        if (_watchModeEnabled)
        {
            return;
        }

        _watchModeEnabled = true;

        try
        {
            await EnsurePackagesAsync();
            FrontendModuleProvider provider = await EnsureProviderAsync();
            _watcher.SetProviderId(provider.Id);
            await _watcher.StartAsync();
        }
        catch
        {
            _watchModeEnabled = false;
            throw;
        }
    }

    public async Task StopWatchAsync()
    {
        _watchModeEnabled = false;
        await _watcher.StopAsync();
    }

    public FrontendHotUpdate? DequeueHotUpdate()
    {
        if (_hotUpdates.TryDequeue(out FrontendHotUpdate? update))
        {
            return update;
        }

        return null;
    }

    private void ResetHotUpdateQueue()
    {
        while (_hotUpdates.TryDequeue(out _))
        {
        }
    }

    private void HandleWatchDiagnostic(FrontendCliDiagnostic diagnostic)
    {
        bool isError = string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase);
        bool isWarning = string.Equals(diagnostic.Severity, "warning", StringComparison.OrdinalIgnoreCase);

        if (isError || isWarning)
        {
            LogDiagnostic(diagnostic);
        }
    }

    private void HandleWatchOutput(string? line, bool isError)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (!isError && line.StartsWith("[webstir-frontend][frontend.watch.", StringComparison.Ordinal))
        {
            return;
        }

        if (isError)
        {
            _logger.LogWarning("[frontend-watch] {Line}", line);
        }
        else
        {
            _logger.LogInformation("[frontend-watch] {Line}", line);
        }
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
            _logger.LogDebug("[frontend] Verifying framework packages...");

            FrontendModuleProvider frontendProvider = await EnsureProviderAsync();
            string frontendProviderId = frontendProvider.Id;
            string? frontendProviderSpec = ProviderSpecOverrides.GetFrontendProviderSpec();
            string? frontendDependencyOverride = ProviderSpecOverrides.GetDefaultProviderSpec(
                frontendProviderId,
                ProviderSpecOverrides.DefaultFrontendProviderId,
                frontendProviderSpec);

            string backendProviderId = ProviderSpecOverrides.ResolveBackendProviderId(_workspace);
            string? backendProviderSpec = ProviderSpecOverrides.GetBackendProviderSpec();
            string? backendDependencyOverride = ProviderSpecOverrides.GetDefaultProviderSpec(
                backendProviderId,
                ProviderSpecOverrides.DefaultBackendProviderId,
                backendProviderSpec);

            string testingProviderId = ProviderSpecOverrides.ResolveTestingProviderId(_workspace);
            string? testingProviderSpec = ProviderSpecOverrides.GetTestingProviderSpec();
            string? testingDependencyOverride = ProviderSpecOverrides.GetDefaultProviderSpec(
                testingProviderId,
                ProviderSpecOverrides.DefaultTestingProviderId,
                testingProviderSpec);

            PackageWorkspaceAdapter workspaceAdapter = new(_workspace);
            PackageManagerDescriptor packageManager = workspaceAdapter.PackageManager;
            WorkspaceProfile profile = _workspace.DetectWorkspaceProfile();
            PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
                workspaceAdapter,
                _logger,
                ensureFrontend: () => FrontendPackageInstaller.EnsureAsync(workspaceAdapter, frontendDependencyOverride),
                ensureTesting: () => TestPackageInstaller.EnsureAsync(workspaceAdapter, testingDependencyOverride),
                ensureBackend: () => BackendPackageInstaller.EnsureAsync(workspaceAdapter, backendDependencyOverride),
                includeFrontend: true,
                includeTesting: true,
                includeBackend: profile.HasBackend,
                autoInstall: true);

            if (summary.InstallPerformed)
            {
                _logger.LogInformation("[frontend] Package dependencies refreshed; {Manager} install completed.", packageManager.Executable);
            }
            else
            {
                _logger.LogDebug("[frontend] Packages already up to date.");
            }

            LogDependencyUpdates(summary);

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
                frontendProviderId,
                frontendProviderSpec,
                ProviderSpecOverrides.DefaultFrontendProviderId,
                "frontend",
                message => _logger.LogInformation(message)).ConfigureAwait(false);

            await ProviderPackageInstaller.EnsureAsync(
                workspaceAdapter,
                testingProviderId,
                testingProviderSpec,
                ProviderSpecOverrides.DefaultTestingProviderId,
                "testing",
                message => _logger.LogInformation(message)).ConfigureAwait(false);

            if (profile.HasBackend)
            {
                await ProviderPackageInstaller.EnsureAsync(
                    workspaceAdapter,
                    backendProviderId,
                    backendProviderSpec,
                    ProviderSpecOverrides.DefaultBackendProviderId,
                    "backend",
                    message => _logger.LogInformation(message)).ConfigureAwait(false);
            }

            _logger.LogDebug("[frontend] Package verification complete.");
            _packagesVerified = true;
        }
        finally
        {
            _packageLock.Release();
        }
    }

    private async Task<FrontendModuleProvider> EnsureProviderAsync()
    {
        if (_resolvedProvider is not null)
        {
            return _resolvedProvider;
        }

        _resolvedProvider = await _moduleProviderResolver.ResolveAsync(_workspace, CancellationToken.None);
        _logger.LogDebug("[frontend] Using module provider {ProviderId}.", _resolvedProvider.Id);
        return _resolvedProvider;
    }

    private void LogModuleBuildResult(string stage, ModuleBuildExecutionResult result)
    {
        if (!_buildSummaryLogged)
        {
            _logger.LogInformation(
                "[frontend] {Stage} provider {ProviderId} produced {EntryCount} entry point(s).",
                stage,
                result.Provider.Id,
                result.Manifest.EntryPoints.Count);
            _buildSummaryLogged = true;
        }
        else
        {
            _logger.LogDebug(
                "[frontend] {Stage} provider {ProviderId} produced {EntryCount} entry point(s).",
                stage,
                result.Provider.Id,
                result.Manifest.EntryPoints.Count);
        }

        if (result.Manifest.EntryPoints.Count > 0)
        {
            _logger.LogDebug(
                "[frontend] Entry points: {EntryPoints}",
                string.Join(", ", result.Manifest.EntryPoints));
        }

        if (result.Manifest.StaticAssets.Count > 0)
        {
            _logger.LogDebug(
                "[frontend] Static assets: {Assets}",
                string.Join(", ", result.Manifest.StaticAssets));
        }

        foreach (ModuleDiagnostic diagnostic in result.Manifest.Diagnostics)
        {
            if (string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[frontend] {Message}", diagnostic.Message);
            }
            else if (string.Equals(diagnostic.Severity, "warn", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[frontend] {Message}", diagnostic.Message);
            }
            else
            {
                _logger.LogDebug("[frontend] {Message}", diagnostic.Message);
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
            _logger.LogError("[frontend] {Message}", evt.Message);
        }
        else if (string.Equals(evt.Type, "warn", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[frontend] {Message}", evt.Message);
        }
        else
        {
            _logger.LogDebug("[frontend] {Message}", evt.Message);
        }
    }

    private void LogDependencyUpdates(PackageEnsureSummary summary)
    {
        if (summary.Frontend is { DependencyUpdated: true } frontend)
        {
            _logger.LogInformation("{Package} dependency updated to {Specifier}.", frontend.Metadata.Name, frontend.Metadata.RegistrySpecifier);
        }

        if (summary.Testing is { DependencyUpdated: true } testing)
        {
            _logger.LogInformation("{Package} dependency updated to {Specifier}.", testing.Metadata.Name, testing.Metadata.RegistrySpecifier);
        }

        if (summary.Backend is { DependencyUpdated: true } backend)
        {
            _logger.LogInformation("{Package} dependency updated to {Specifier}.", backend.Metadata.Name, backend.Metadata.RegistrySpecifier);
        }
    }

    private void ThrowMismatch(PackageEnsureSummary summary)
    {
        List<string> mismatches = [];

        if (summary.Frontend is { VersionMismatch: true } frontend)
        {
            string installed = string.IsNullOrWhiteSpace(frontend.InstalledVersion)
                ? "missing"
                : frontend.InstalledVersion!;
            _logger.LogWarning(
                "{Package} {InstalledVersion} detected but {ExpectedVersion} is bundled. Run '{Command} install' to refresh dependencies.",
                frontend.Metadata.Name,
                installed,
                frontend.Metadata.Version,
                App.Name);
            mismatches.Add($"{frontend.Metadata.Name} (found {installed}, expected {frontend.Metadata.Version})");
        }

        if (summary.Testing is { VersionMismatch: true } testing)
        {
            string installed = string.IsNullOrWhiteSpace(testing.InstalledVersion)
                ? "missing"
                : testing.InstalledVersion!;
            _logger.LogWarning(
                "{Package} {InstalledVersion} detected but {ExpectedVersion} is bundled. Run '{Command} install' to refresh dependencies.",
                testing.Metadata.Name,
                installed,
                testing.Metadata.Version,
                App.Name);
            mismatches.Add($"{testing.Metadata.Name} (found {installed}, expected {testing.Metadata.Version})");
        }

        if (summary.Backend is { VersionMismatch: true } backend)
        {
            string installed = string.IsNullOrWhiteSpace(backend.InstalledVersion)
                ? "missing"
                : backend.InstalledVersion!;
            _logger.LogWarning(
                "{Package} {InstalledVersion} detected but {ExpectedVersion} is bundled. Run '{Command} install' to refresh dependencies.",
                backend.Metadata.Name,
                installed,
                backend.Metadata.Version,
                App.Name);
            mismatches.Add($"{backend.Metadata.Name} (found {installed}, expected {backend.Metadata.Version})");
        }

        if (mismatches.Count == 0)
        {
            return;
        }

        string details = string.Join(", ", mismatches);
        throw new InvalidOperationException($"Framework packages are out of sync: {details}. Run '{App.Name} install' to synchronize dependencies.");
    }

    private async Task RunFrontendCliAsync(string command, string? changedFile, params string[] extraArgs)
    {
        NodeRuntime.EnsureMinimumVersion();
        string executable = GetExecutablePath();
        bool useNodeLauncher = false;
        string? cliScriptPath = null;
        bool execViaNpmExec = false;
        if (!File.Exists(executable))
        {
            // Fallback: resolve CLI script from installed package when .bin link is missing
            cliScriptPath = TryResolveCliScriptPath();
            if (!string.IsNullOrEmpty(cliScriptPath) && File.Exists(cliScriptPath))
            {
                useNodeLauncher = true;
            }
            else
            {
                // Final fallback: run via `npm exec` with explicit version
                execViaNpmExec = true;
            }
        }

        ProcessStartInfo psi = new()
        {
            FileName = execViaNpmExec ? "npm" : (useNodeLauncher ? "node" : executable),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _workspace.WorkingPath
        };

        if (execViaNpmExec)
        {
            string spec = $"{Framework.Packaging.FrameworkPackageCatalog.Frontend.Name}@{Framework.Packaging.FrameworkPackageCatalog.Frontend.Version}";
            psi.ArgumentList.Add("exec");
            psi.ArgumentList.Add("--yes");
            psi.ArgumentList.Add(spec);
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add(command);
        }
        else
        {
            if (useNodeLauncher && !string.IsNullOrEmpty(cliScriptPath))
            {
                psi.ArgumentList.Add(cliScriptPath);
            }

            psi.ArgumentList.Add(command);
        }
        foreach (string extra in extraArgs)
        {
            psi.ArgumentList.Add(extra);
        }

        psi.ArgumentList.Add("--workspace");
        psi.ArgumentList.Add(_workspace.WorkingPath);

        if (!string.IsNullOrWhiteSpace(changedFile))
        {
            psi.ArgumentList.Add("--changed-file");
            psi.ArgumentList.Add(changedFile!);
        }


        using Process process = new()
        {
            StartInfo = psi
        };
        process.OutputDataReceived += (_, args) => HandleCliOutput(args.Data, isError: false);
        process.ErrorDataReceived += (_, args) => HandleCliOutput(args.Data, isError: true);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"webstir-frontend {command} failed with exit code {process.ExitCode}.");
        }
    }

    private string GetExecutablePath()
    {
        string binDirectory = Path.Combine(_workspace.NodeModulesPath, ".bin");
        string executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "webstir-frontend.cmd"
            : "webstir-frontend";
        return Path.Combine(binDirectory, executable);
    }

    private string? TryResolveCliScriptPath()
    {
        try
        {
            string scopePath = Path.Combine(_workspace.NodeModulesPath, "@webstir-io");
            if (!Directory.Exists(scopePath))
            {
                return null;
            }

            static string? Probe(string directory)
            {
                string candidate = Path.Combine(directory, "dist", "cli.js");
                return File.Exists(candidate) ? candidate : null;
            }

            string direct = Path.Combine(scopePath, "webstir-frontend");
            if (Directory.Exists(direct))
            {
                string? hit = Probe(direct);
                if (hit is not null)
                    return hit;
            }

            foreach (string dir in Directory.GetDirectories(scopePath, "webstir-frontend@*", SearchOption.TopDirectoryOnly))
            {
                string? hit = Probe(dir);
                if (hit is not null)
                    return hit;
            }

            foreach (string dir in Directory.GetDirectories(scopePath, ".webstir-frontend-*", SearchOption.TopDirectoryOnly))
            {
                string? hit = Probe(dir);
                if (hit is not null)
                    return hit;
            }

            string nested = Path.Combine(scopePath, "node_modules", "webstir-frontend");
            if (Directory.Exists(nested))
            {
                string? hit = Probe(nested);
                if (hit is not null)
                    return hit;
            }
        }
        catch
        {
            // best-effort resolution
        }

        return null;
    }

    private static bool IsVerboseWatchLoggingEnabled()
    {
        string? value = Environment.GetEnvironmentVariable("WEBSTIR_FRONTEND_WATCH_VERBOSE");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHmrVerboseLoggingEnabled()
    {
        string? value = Environment.GetEnvironmentVariable("WEBSTIR_FRONTEND_HMR_VERBOSE");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private async Task LogPublishManifestAsync()
    {
        try
        {
            FrontendManifest manifest = await FrontendManifestLoader.LoadAsync(_workspace);
            _logger.LogInformation(
                "Frontend publish outputs located at {DistPath}",
                manifest.Paths.Dist.Frontend);
            _logger.LogInformation(
                "Frontend features: htmlSecurity={HtmlSecurity}, imageOptimization={ImageOptimization}, precompression={Precompression}",
                manifest.Features.HtmlSecurity,
                manifest.Features.ImageOptimization,
                manifest.Features.Precompression);
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning(
                "Frontend manifest not found after publish. Run the frontend CLI to regenerate outputs.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or IOException)
        {
            _logger.LogWarning(
                ex,
                "Failed to read frontend manifest after publish.");
        }
    }

    private void HandleCliOutput(string? line, bool isError)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (TryParseDiagnostic(line, out FrontendCliDiagnostic? diagnostic))
        {
            LogDiagnostic(diagnostic!);
            return;
        }

        if (isError)
        {
            _logger.LogWarning("[frontend] {Line}", line);
        }
        else
        {
            _logger.LogInformation("[frontend] {Line}", line);
        }
    }

    private void LogDiagnostic(FrontendCliDiagnostic diagnostic)
    {
        LogLevel level = diagnostic.Severity?.ToLowerInvariant() switch
        {
            "error" => LogLevel.Error,
            "warning" => LogLevel.Warning,
            _ => LogLevel.Information
        };

        string code = string.IsNullOrWhiteSpace(diagnostic.Code) ? diagnostic.Kind : diagnostic.Code;

        if (diagnostic.Data is { Count: > 0 })
        {
            string serializedData = JsonSerializer.Serialize(diagnostic.Data, DiagnosticSerializerOptions);
            if (string.IsNullOrWhiteSpace(diagnostic.Stage))
            {
                _logger.Log(level, "[frontend][{Code}] {Message} | data: {Data}", code, diagnostic.Message, serializedData);
            }
            else
            {
                _logger.Log(level, "[frontend][{Code}] {Message} (stage: {Stage}) | data: {Data}", code, diagnostic.Message, diagnostic.Stage, serializedData);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(diagnostic.Stage))
            {
                _logger.Log(level, "[frontend][{Code}] {Message}", code, diagnostic.Message);
            }
            else
            {
                _logger.Log(level, "[frontend][{Code}] {Message} (stage: {Stage})", code, diagnostic.Message, diagnostic.Stage);
            }
        }

        if (!string.IsNullOrWhiteSpace(diagnostic.Suggestion))
        {
            _logger.Log(level, "[frontend][{Code}] Suggestion: {Suggestion}", code, diagnostic.Suggestion);
        }
    }

    private static bool TryParseDiagnostic(string line, out FrontendCliDiagnostic? diagnostic)
    {
        if (!line.StartsWith(DiagnosticPrefix, StringComparison.Ordinal))
        {
            diagnostic = null;
            return false;
        }

        string json = line[DiagnosticPrefix.Length..];

        try
        {
            diagnostic = JsonSerializer.Deserialize<FrontendCliDiagnostic>(json, DiagnosticSerializerOptions);
            if (diagnostic is null)
            {
                return false;
            }

            return string.Equals(diagnostic.Type, "diagnostic", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            diagnostic = null;
            return false;
        }
    }

}
