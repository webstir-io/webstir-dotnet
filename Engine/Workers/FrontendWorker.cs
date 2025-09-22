using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text.Json;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Microsoft.Extensions.Logging;

namespace Engine.Workers;

public sealed class FrontendWorker(
    AppWorkspace workspace,
    ILogger<FrontendWorker> logger) : IFrontendWorker
{
    private readonly AppWorkspace _workspace = workspace;
    private readonly ILogger<FrontendWorker> _logger = logger;

    private const string DiagnosticPrefix = "WEBSTIR_DIAGNOSTIC ";
    private static readonly JsonSerializerOptions DiagnosticSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public int BuildOrder => 1;

    public async Task InitAsync(ProjectMode mode) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.FrontendPath, _workspace.FrontendPath);

    public async Task BuildAsync(string? changedFilePath = null)
    {
        await EnsurePackagesAsync();
        string command = string.IsNullOrWhiteSpace(changedFilePath) ? "build" : "rebuild";
        await RunFrontendCliAsync(command, changedFilePath);
    }

    public async Task PublishAsync()
    {
        await EnsurePackagesAsync();
        await RunFrontendCliAsync("publish", null);
    }

    public async Task AddPageAsync(string pageName) => await RunFrontendCliAsync("add-page", null, pageName);

    private async Task EnsurePackagesAsync()
    {
        FrontendPackageEnsureResult frontendResult = await FrontendPackageInstaller.EnsureAsync(_workspace);
        PackageEnsureResult testResult = await TestPackageInstaller.EnsureAsync(_workspace);

        bool installRequired = frontendResult.ToolsAdded || frontendResult.DependencyUpdated || frontendResult.TarballUpdated
            || testResult.ToolsAdded || testResult.DependencyUpdated || testResult.TarballUpdated;

        if (installRequired)
        {
            NpmHelper.RunNpmInstall(_workspace.WorkingPath);
            frontendResult = await FrontendPackageInstaller.EnsureAsync(_workspace);
            testResult = await TestPackageInstaller.EnsureAsync(_workspace);
        }

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

    private string GetExecutablePath()
    {
        string binDirectory = Path.Combine(_workspace.NodeModulesPath, ".bin");
        string executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "webstir-frontend.cmd"
            : "webstir-frontend";
        return Path.Combine(binDirectory, executable);
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

    private sealed class FrontendCliDiagnostic
    {
        public string Type { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Stage { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public Dictionary<string, JsonElement>? Data
        {
            get; init;
        }
        public string? Suggestion
        {
            get; init;
        }
    }
}
