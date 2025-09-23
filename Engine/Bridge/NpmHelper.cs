using System;
using System.Diagnostics;
using Engine.Extensions;

namespace Engine.Bridge;

public static class NpmHelper
{
    public static void RunNpmInstall(string workingPath)
    {
        string packageLockPath = workingPath.Combine(Files.PackageLockJson);
        string npmCommand = packageLockPath.Exists() ? "ci" : "install";

        ProcessStartInfo processInfo = new()
        {
            FileName = "npm",
            Arguments = npmCommand,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingPath
        };

        using Process process = Process.Start(processInfo)
            ?? throw new Exception("Failed to start npm install process.");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string errors = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();
            string errorMessage = $"npm install failed (Exit Code: {process.ExitCode})";
            if (!string.IsNullOrWhiteSpace(errors))
            {
                errorMessage += $"\nErrors:\n{errors}";
            }
            if (!string.IsNullOrWhiteSpace(output))
            {
                errorMessage += $"\nOutput:\n{output}";
            }
            throw new Exception(errorMessage);
        }
    }
}
