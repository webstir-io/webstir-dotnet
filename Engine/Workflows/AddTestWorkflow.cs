using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Engine.Extensions;
using System.Collections.Generic;
using Engine.Helpers;
using Engine.Interfaces;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Engine.Workflows;

public sealed class AddTestWorkflow(AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers) : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.AddTest;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string[] filtered = [.. args.Where(arg => arg != WorkflowName)];
        string? nameOrPath = filtered.FirstOrDefault(arg => !arg.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(nameOrPath))
        {
            Console.WriteLine("Please specify a test name or path. See 'webstir help add-test'.");
            return;
        }

        await EnsurePackageAsync();
        await RunTestCliAsync(nameOrPath.Trim());
    }

    private async Task EnsurePackageAsync()
    {
        PackageEnsureResult result = await TestPackageInstaller.EnsureAsync(Context);

        if (result.ToolsAdded || result.DependencyUpdated || result.TarballUpdated)
        {
            NpmHelper.RunNpmInstall(Context.WorkingPath);
            result = await TestPackageInstaller.EnsureAsync(Context);
        }

        if (result.ToolsAdded)
        {
            Console.WriteLine($"Added testing package archive: {Path.Combine(Folders.Tools, result.Metadata.FileName)}");
        }

        if (result.DependencyUpdated)
        {
            Console.WriteLine($"Pinned @webstir/test to {result.Metadata.Dependency} in {Files.PackageJson}");
        }

        if (result.TarballUpdated)
        {
            Console.WriteLine("Updated @webstir/test tarball; npm install rerun may be required.");
        }

        if (result.VersionMismatch)
        {
            string installed = string.IsNullOrWhiteSpace(result.InstalledVersion)
                ? "not installed"
                : $"{result.InstalledVersion}";
            Console.WriteLine($"Warning: @webstir/test {installed} differs from packaged {result.Metadata.Version}. Run 'npm install' to refresh node_modules.");
        }
    }
    private async Task RunTestCliAsync(string name)
    {
        string executable = GetExecutablePath();
        if (!File.Exists(executable))
        {
            throw new InvalidOperationException($"webstir-test-add executable not found at {executable}. Run npm install to restore dependencies.");
        }

        ProcessStartInfo psi = new()
        {
            FileName = executable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Context.WorkingPath
        };

        psi.ArgumentList.Add(name);
        psi.ArgumentList.Add("--workspace");
        psi.ArgumentList.Add(Context.WorkingPath);

        using Process process = new()
        {
            StartInfo = psi
        };
        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                Console.WriteLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                Console.WriteLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"webstir-test-add failed with exit code {process.ExitCode}.");
        }
    }

    private string GetExecutablePath()
    {
        string executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "webstir-test-add.cmd"
            : "webstir-test-add";
        return Path.Combine(Context.NodeModulesPath, ".bin", executable);
    }
}
