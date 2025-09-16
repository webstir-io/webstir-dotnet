using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Core.Esbuild;

public class CssAutoprefixer(AppWorkspace workspace)
{
    private readonly AppWorkspace _workspace = workspace;

    public async Task<bool> ApplyAsync(string cssPath, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(cssPath);

        if (!File.Exists(cssPath))
        {
            logger?.LogError("[CSS] Autoprefixer input not found: {Path}", cssPath);
            return false;
        }

        string autoprefixerPath = GetAutoprefixerPath();

        ProcessStartInfo psi = new()
        {
            FileName = autoprefixerPath,
            Arguments = FormattableString.Invariant($"\"{cssPath}\" --replace"),
            WorkingDirectory = _workspace.WorkingPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start autoprefixer process");

        string stdOut = await process.StandardOutput.ReadToEndAsync();
        string stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            string error = string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr;
            logger?.LogError("[CSS] Autoprefixer failed for {Path} - {Message}", cssPath, error.Trim());
            return false;
        }

        return true;
    }

    private string GetAutoprefixerPath()
    {
        string executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "autoprefixer.cmd"
            : "autoprefixer";

        string autoprefixerPath = Path.Combine(
            _workspace.NodeModulesPath,
            EsbuildConstants.BinFolder,
            executable);

        if (!File.Exists(autoprefixerPath))
        {
            throw new FileNotFoundException($"autoprefixer not found at {autoprefixerPath}. Please run 'npm install'.");
        }

        return autoprefixerPath;
    }
}

