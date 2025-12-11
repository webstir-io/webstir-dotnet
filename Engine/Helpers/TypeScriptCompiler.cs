using System;
using System.Diagnostics;
using System.IO;
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

        ProcessStartInfo startInfo = new()
        {
            FileName = "tsc",
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

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"TypeScript compilation failed with exit code {process.ExitCode}.");
        }
    }
}
