using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Engine.Extensions;
using Utilities.ProcessRunner;

namespace Engine.Bridge;

public static class NpmHelper
{
    public static async Task RunNpmInstallAsync(string workingPath, CancellationToken cancellationToken = default)
    {
        string packageLockPath = workingPath.Combine(Files.PackageLockJson);
        string npmCommand = packageLockPath.Exists() ? "ci" : "install";

        ProcessRunner runner = new();
        ProcessSpec spec = CreateInstallSpec(workingPath, npmCommand);

        ProcessResult result = await runner.RunAsync(spec, cancellationToken).ConfigureAwait(false);

        if (!result.CompletedSuccessfully && npmCommand == "ci")
        {
            DeleteIfExists(packageLockPath);
            spec = CreateInstallSpec(workingPath, "install");
            result = await runner.RunAsync(spec, cancellationToken).ConfigureAwait(false);
            if (!result.CompletedSuccessfully)
            {
                throw CreateInstallException(result, "npm install");
            }
            return;
        }

        if (!result.CompletedSuccessfully)
        {
            throw CreateInstallException(result, $"npm {npmCommand}");
        }
    }

    public static async Task RunNpmInstallPackagesAsync(string workingPath, params string[] packageSpecs)
    {
        ArgumentNullException.ThrowIfNull(workingPath);
        ArgumentNullException.ThrowIfNull(packageSpecs);

        ProcessRunner runner = new();
        ProcessSpec spec = new()
        {
            FileName = "npm",
            Arguments = BuildInstallArguments(packageSpecs),
            WorkingDirectory = workingPath,
            ExitTimeout = TimeSpan.FromMinutes(10)
        };

        ProcessResult result = await runner.RunAsync(spec).ConfigureAwait(false);

        if (!result.CompletedSuccessfully)
        {
            throw CreateInstallException(result, "npm install (explicit packages)");
        }
    }

    private static ProcessSpec CreateInstallSpec(string workingPath, string command)
    {
        ProcessSpec spec = new()
        {
            FileName = "npm",
            Arguments = command,
            WorkingDirectory = workingPath,
            ExitTimeout = TimeSpan.FromMinutes(10)
        };

        return spec;
    }

    private static string BuildInstallArguments(string[] packageSpecs)
    {
        System.Text.StringBuilder builder = new();
        builder.Append("install --no-save");

        foreach (string spec in packageSpecs)
        {
            builder.Append(' ');
            builder.Append(spec);
        }

        return builder.ToString();
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

    private static Exception CreateInstallException(ProcessResult result, string description)
    {
        string errors = result.StandardError;
        string output = result.StandardOutput;
        string errorMessage = $"{description} failed (Exit Code: {result.ExitCode})";
        if (!string.IsNullOrWhiteSpace(errors))
        {
            errorMessage += $"\nErrors:\n{errors}";
        }
        if (!string.IsNullOrWhiteSpace(output))
        {
            errorMessage += $"\nOutput:\n{output}";
        }

        if (ContainsRegistryAuthHint(errors) || ContainsRegistryAuthHint(output))
        {
            errorMessage += "\nHint: Ensure GH_PACKAGES_TOKEN is set and .npmrc permits access to https://npm.pkg.github.com/@webstir-io.";
        }

        return new Exception(errorMessage);
    }

    private static bool ContainsRegistryAuthHint(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return content.Contains("npm.pkg.github.com", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("E401", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("E402", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("E403", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("E404", StringComparison.OrdinalIgnoreCase);
    }
}
