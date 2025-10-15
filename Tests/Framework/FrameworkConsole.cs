using System;
using System.IO;
using Tests;

namespace Tests.Framework;

public sealed class FrameworkConsole
{
    private static string FrameworkProjectPath => Path.Combine(Paths.RepositoryRoot, "Framework", "Framework.csproj");

    public ProcessRunner.ProcessResult Run(
        string arguments,
        string? workingDirectory = null,
        int timeoutMs = 10000)
    {
        return ProcessRunner.Run(new ProcessRunOptions
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{FrameworkProjectPath}\" -- {arguments}",
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            ExitTimeoutMs = timeoutMs
        });
    }
}
