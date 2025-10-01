using System;
using System.Diagnostics;
using System.IO;
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

        if (process.ExitCode != 0 && npmCommand == "ci")
        {
            DeleteIfExists(packageLockPath);
            processInfo.Arguments = "install";
            using Process retryProcess = Process.Start(processInfo)
                ?? throw new Exception("Failed to start npm install process.");
            retryProcess.WaitForExit();
            if (retryProcess.ExitCode != 0)
            {
                throw CreateInstallException(retryProcess);
            }
            return;
        }

        if (process.ExitCode != 0)
        {
            throw CreateInstallException(process);
        }
    }

    public static void RunNpmInstallPackages(string workingPath, params string[] packageSpecs)
    {
        ArgumentNullException.ThrowIfNull(workingPath);
        ArgumentNullException.ThrowIfNull(packageSpecs);

        ProcessStartInfo processInfo = new()
        {
            FileName = "npm",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingPath
        };

        processInfo.ArgumentList.Add("install");
        processInfo.ArgumentList.Add("--no-save");
        foreach (string spec in packageSpecs)
        {
            processInfo.ArgumentList.Add(spec);
        }

        using Process? process = Process.Start(processInfo);
        if (process is null)
        {
            throw new Exception("Failed to start npm install (explicit packages) process.");
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw CreateInstallException(process);
        }
    }

    private static void DeleteIfExists(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
            // best effort
        }
        catch (UnauthorizedAccessException)
        {
            // best effort
        }
    }

    private static Exception CreateInstallException(Process process)
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
        return new Exception(errorMessage);
    }
}
