using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
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

    public FrontendWorker(AppWorkspace workspace, ILogger<FrontendWorker> logger)
    {
        _workspace = workspace;
        _logger = logger;
        bool verboseLogging = IsVerboseWatchLoggingEnabled();
        bool hmrVerboseLogging = IsHmrVerboseLoggingEnabled();

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

    public async Task InitAsync(ProjectMode mode) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.FrontendPath, _workspace.FrontendPath);

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
        string command = string.IsNullOrWhiteSpace(changedFilePath) ? "build" : "rebuild";
        await RunFrontendCliAsync(command, changedFilePath);
    }

    public async Task PublishAsync()
    {
        await EnsurePackagesAsync();
        await RunFrontendCliAsync("publish", null);
        await LogPublishManifestAsync();
    }

    public async Task AddPageAsync(string pageName) => await RunFrontendCliAsync("add-page", null, pageName);

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
            _logger.LogInformation("[frontend] Verifying framework packages...");

            PackageWorkspaceAdapter workspaceAdapter = new(_workspace);
            PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
                workspaceAdapter,
                _logger,
                ensureFrontend: preferRegistry => FrontendPackageInstaller.EnsureAsync(workspaceAdapter, preferRegistry),
                ensureTesting: preferRegistry => TestPackageInstaller.EnsureAsync(workspaceAdapter, preferRegistry),
                includeFrontend: true,
                includeTesting: true,
                autoInstall: true);

            if (summary.InstallPerformed)
            {
                _logger.LogInformation("[frontend] Package dependencies refreshed; npm install completed.");
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

            _logger.LogInformation("[frontend] Package verification complete.");
            _packagesVerified = true;
        }
        finally
        {
            _packageLock.Release();
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
            string scopePath = Path.Combine(_workspace.NodeModulesPath, "@electric-coding-llc");
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
