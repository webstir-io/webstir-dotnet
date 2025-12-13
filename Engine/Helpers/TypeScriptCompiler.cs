using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Engine.Extensions;

namespace Engine.Helpers;

internal static class TypeScriptCompiler
{
    internal static async Task CompileAsync(AppWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        string tsConfigPath = workspace.WorkingPath.Combine(Files.BaseTsConfigJson);
        if (!File.Exists(tsConfigPath))
        {
            return;
        }

        string localTscName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "tsc.cmd"
            : "tsc";
        string localTscPath = Path.Combine(workspace.WorkingPath, Folders.NodeModules, ".bin", localTscName);
        string tscExecutable = File.Exists(localTscPath)
            ? localTscPath
            : "tsc";

        ProcessStartInfo startInfo = new()
        {
            FileName = tscExecutable,
            Arguments = $"--build \"{tsConfigPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workspace.WorkingPath
        };

        using Process process = new()
        {
            StartInfo = startInfo
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
                Console.Error.WriteLine(args.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"TypeScript compiler not found. Install 'typescript' (run '{App.Name} install' or add it to devDependencies).",
                ex);
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"TypeScript compilation failed with exit code {process.ExitCode}.");
        }
    }
}
