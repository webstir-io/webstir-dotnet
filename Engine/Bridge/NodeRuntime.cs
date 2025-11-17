using System;
using Utilities.Process;

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
        ProcessRunner runner = new();
        ProcessSpec spec = new()
        {
            FileName = "node",
            Arguments = "--version",
            ExitTimeout = TimeSpan.FromSeconds(10)
        };

        ProcessResult result;

        try
        {
            result = runner.RunAsync(spec).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Unable to execute 'node --version'. Verify that Node.js is installed and available on PATH.", ex);
        }

        if (!result.CompletedSuccessfully)
        {
            string message = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"'node --version' exited with code {result.ExitCode}."
                : result.StandardError.Trim();
            throw new InvalidOperationException($"Unable to determine Node.js version: {message}");
        }

        string output = result.StandardOutput.Trim();
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
