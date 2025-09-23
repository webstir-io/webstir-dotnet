using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Engine.Bridge.Test;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
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
    private readonly SemaphoreSlim _toolchainLock = new(1, 1);
    private bool _toolchainVerified;

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
        await _toolchainLock.WaitAsync();
        try
        {
            if (_toolchainVerified)
            {
                return;
            }

            NodeRuntime.EnsureMinimumVersion();
            _logger.LogInformation("[frontend] Verifying toolchain packages...");
            FrontendPackageEnsureResult frontendResult = await FrontendPackageInstaller.EnsureAsync(_workspace);
            PackageEnsureResult testResult = await TestPackageInstaller.EnsureAsync(_workspace);

            bool installRequired = frontendResult.ToolsAdded || frontendResult.DependencyUpdated || frontendResult.TarballUpdated
                || testResult.ToolsAdded || testResult.DependencyUpdated || testResult.TarballUpdated;

            if (installRequired)
            {
                if (frontendResult.TarballUpdated || testResult.TarballUpdated)
                {
                    RemovePackageLockIfPresent();
                    RemoveCachedPackage("@webstir/frontend");
                    RemoveCachedPackage("@webstir/test");
                }

                _logger.LogInformation("[frontend] Toolchain changed; running npm install...");
                NpmHelper.RunNpmInstall(_workspace.WorkingPath);
                frontendResult = await FrontendPackageInstaller.EnsureAsync(_workspace);
                testResult = await TestPackageInstaller.EnsureAsync(_workspace);
            }
            else
            {
                _logger.LogDebug("[frontend] Toolchain already up to date.");
            }

            _logger.LogInformation("[frontend] Toolchain verification complete.");

            LogVersionMismatch(frontendResult.VersionMismatch, frontendResult.InstalledVersion, frontendResult.Metadata.Name, frontendResult.Metadata.Version);
            LogVersionMismatch(testResult.VersionMismatch, testResult.InstalledVersion, testResult.Metadata.Name, testResult.Metadata.Version);

            if (frontendResult.TarballUpdated)
            {
                _logger.LogInformation("{Package} tarball updated; run npm install if changes are not applied automatically.", frontendResult.Metadata.Name);
            }

            if (testResult.TarballUpdated)
            {
                _logger.LogInformation("{Package} tarball updated; run npm install if changes are not applied automatically.", testResult.Metadata.Name);
            }

            _toolchainVerified = true;
        }
        finally
        {
            _toolchainLock.Release();
        }
    }

    private void LogVersionMismatch(bool mismatch, string? installedVersion, string packageName, string expectedVersion)
    {
        if (!mismatch)
        {
            return;
        }

        string installed = string.IsNullOrWhiteSpace(installedVersion) ? "missing" : installedVersion!;
        _logger.LogWarning(
            "{Package} {InstalledVersion} detected but {ExpectedVersion} is bundled. Run npm install to refresh dependencies.",
            packageName,
            installed,
            expectedVersion);
    }

    private async Task RunFrontendCliAsync(string command, string? changedFile, params string[] extraArgs)
    {
        NodeRuntime.EnsureMinimumVersion();
        string executable = GetExecutablePath();
        if (!File.Exists(executable))
        {
            throw new InvalidOperationException($"webstir-frontend executable not found at {executable}. Run npm install to restore dependencies.");
        }

        ProcessStartInfo psi = new()
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _workspace.WorkingPath
        };

        psi.ArgumentList.Add(command);
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

    private void RemovePackageLockIfPresent()
    {
        try
        {
            string packageLockPath = Path.Combine(_workspace.WorkingPath, Files.PackageLockJson);
            if (File.Exists(packageLockPath))
            {
                File.Delete(packageLockPath);
            }
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Failed to remove package-lock.json while refreshing the frontend toolchain.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Insufficient permissions to remove package-lock.json while refreshing the frontend toolchain.");
        }
    }

    private void RemoveCachedPackage(string packageName)
    {
        try
        {
            string packagePath = Path.Combine(_workspace.NodeModulesPath, packageName); // Handles scoped packages
            if (Directory.Exists(packagePath))
            {
                Directory.Delete(packagePath, recursive: true);
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Nothing to remove.
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Failed to remove cached package {Package} while refreshing the frontend toolchain.", packageName);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Insufficient permissions to remove cached package {Package} while refreshing the frontend toolchain.", packageName);
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
