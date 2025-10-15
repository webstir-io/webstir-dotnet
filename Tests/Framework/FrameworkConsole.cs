using System;
using System.IO;

namespace Tests.Framework;

public sealed class FrameworkConsole
{
    private static string FrameworkBinaryPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Framework.dll");

    public ProcessRunner.ProcessResult Run(
        string arguments,
        string? workingDirectory = null,
        int timeoutMs = 10000)
    {
        return ProcessRunner.Run(new ProcessRunOptions
        {
            FileName = "dotnet",
            Arguments = $"\"{FrameworkBinaryPath}\" {arguments}",
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            ExitTimeoutMs = timeoutMs
        });
    }
}
