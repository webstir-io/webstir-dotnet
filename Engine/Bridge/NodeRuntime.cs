using System;
using System.Diagnostics;

namespace Engine.Bridge;

internal static class NodeRuntime
{
    private static readonly Version MinimumSupportedVersion = new(20, 18, 1);
    private static readonly Lazy<Version> CachedVersion = new(GetNodeVersion, isThreadSafe: true);

    internal static Version EnsureMinimumVersion()
    {
        Version version = CachedVersion.Value;

        if (version < MinimumSupportedVersion)
        {
            throw new InvalidOperationException($"Node.js {MinimumSupportedVersion} or newer is required to run Webstir frontend tooling. Detected Node.js {version}.");
        }

        return version;
    }

    private static Version GetNodeVersion()
    {
        ProcessStartInfo processInfo = new()
        {
            FileName = "node",
            Arguments = "--version",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(processInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Unable to execute 'node --version'. Verify that Node.js is installed and available on PATH.");
        }

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string message = string.IsNullOrWhiteSpace(standardError)
                ? $"'node --version' exited with code {process.ExitCode}."
                : standardError.Trim();
            throw new InvalidOperationException($"Unable to determine Node.js version: {message}");
        }

        string output = standardOutput.Trim();
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException("Unable to determine Node.js version: 'node --version' produced no output.");
        }

        string normalized = output.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? output[1..]
            : output;

        if (!Version.TryParse(normalized, out Version? version))
        {
            throw new InvalidOperationException($"Unable to parse Node.js version from '{output}'.");
        }

        return version;
    }
}
