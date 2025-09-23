using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Engine.Bridge;
using Engine.Bridge.Test;
using Engine.Helpers;
using Engine.Interfaces;

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
        PackageEnsureResult result = await TestPackageUtilities.EnsurePackageAsync(Context);
        TestPackageUtilities.LogEnsureMessages(result);
    }
    private async Task RunTestCliAsync(string name)
    {
        NodeRuntime.EnsureMinimumVersion();
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
