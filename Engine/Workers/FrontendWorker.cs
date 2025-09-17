using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Engine.Pipelines.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Engine.Workers;

public sealed class FrontendWorker(
    AppWorkspace workspace,
    IEnumerable<IFrontendHandler> frontendHandlers,
    ILogger<FrontendWorker> logger) : IFrontendWorker
{
    private readonly AppWorkspace _workspace = workspace;
    private readonly ILogger<FrontendWorker> _logger = logger;
    private readonly IEnumerable<IFrontendHandler> _frontendHandlers = frontendHandlers;

    public int BuildOrder => 1;

    public async Task InitAsync(ProjectMode mode) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.FrontendPath, _workspace.FrontendPath);

    public async Task BuildAsync(string? changedFilePath = null)
    {
        await EnsurePackagesAsync();
        await RunFrontendCliAsync("build", changedFilePath);
    }

    public async Task PublishAsync()
    {
        await EnsurePackagesAsync();
        await RunFrontendCliAsync("publish", null);
    }

    public async Task AddPageAsync(string pageName)
    {
        List<Task<bool>> tasks = [];
        foreach (IPageHandler handler in _frontendHandlers.OfType<IPageHandler>())
        {
            tasks.Add(handler.AddPageAsync(pageName));
        }

        bool[] results = await Task.WhenAll(tasks);
        if (!results.All(r => r))
        {
            _logger.LogError("Failed to add page {PageName}", pageName);
        }
    }

    private async Task EnsurePackagesAsync()
    {
        FrontendPackageEnsureResult frontendResult = await FrontendPackageInstaller.EnsureAsync(_workspace);
        PackageEnsureResult testResult = await TestPackageInstaller.EnsureAsync(_workspace);

        bool installRequired = frontendResult.ToolsAdded || frontendResult.DependencyUpdated
            || testResult.ToolsAdded || testResult.DependencyUpdated;

        if (installRequired)
        {
            NpmHelper.RunNpmInstall(_workspace.WorkingPath);
            frontendResult = await FrontendPackageInstaller.EnsureAsync(_workspace);
            testResult = await TestPackageInstaller.EnsureAsync(_workspace);
        }

        LogVersionMismatch(frontendResult.VersionMismatch, frontendResult.InstalledVersion, frontendResult.Metadata.Name, frontendResult.Metadata.Version);
        LogVersionMismatch(testResult.VersionMismatch, testResult.InstalledVersion, testResult.Metadata.Name, testResult.Metadata.Version);
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

    private async Task RunFrontendCliAsync(string command, string? changedFile)
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
        psi.ArgumentList.Add("--workspace");
        psi.ArgumentList.Add(_workspace.WorkingPath);

        if (!string.IsNullOrWhiteSpace(changedFile))
        {
            psi.ArgumentList.Add("--changed-file");
            psi.ArgumentList.Add(changedFile!);
        }

        using Process process = new() { StartInfo = psi };
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                _logger.LogInformation("[frontend] {Line}", args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                _logger.LogWarning("[frontend] {Line}", args.Data);
            }
        };

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
}
