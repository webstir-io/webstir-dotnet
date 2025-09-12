using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Engine.Pipelines.Fonts;

public static class FontOptimizer
{
    private static readonly string[] FontExts = [
        FileExtensions.Woff2, FileExtensions.Woff, FileExtensions.Ttf,
        FileExtensions.Otf, FileExtensions.Eot, FileExtensions.Svg
    ];

    public static async Task OptimizeAsync(string sourceRoot, string destRoot)
    {
        ArgumentNullException.ThrowIfNull(sourceRoot);
        ArgumentNullException.ThrowIfNull(destRoot);

        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        string[] files = [.. Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories).Where(f => FontExts.Contains(Path.GetExtension(f).ToLowerInvariant()))];

        foreach (string srcFile in files)
        {
            string relativePath = Path.GetRelativePath(sourceRoot, srcFile);
            string destPath = Path.Combine(destRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            File.Copy(srcFile, destPath, true);

            string ext = Path.GetExtension(srcFile).ToLowerInvariant();
            if (string.Equals(ext, FileExtensions.Ttf, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, FileExtensions.Otf, StringComparison.OrdinalIgnoreCase))
            {
                await TryCreateWoff2Async(destPath);
            }
        }
    }

    private static async Task TryCreateWoff2Async(string sourcePath)
    {
        if (!IsToolAvailable("woff2_compress"))
        {
            return;
        }

        string destPath = Path.ChangeExtension(sourcePath, FileExtensions.Woff2);
        if (File.Exists(destPath))
        {
            return;
        }

        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "woff2_compress",
                Arguments = FormattableString.Invariant($"\"{sourcePath}\""),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(sourcePath)!
            };
            using Process p = Process.Start(psi)!;
            await p.WaitForExitAsync();
            // Tool writes output next to source with .woff2 extension
        }
        catch
        {
            // ignore failures; original font remains
        }
    }

    private static bool IsToolAvailable(string tool)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = tool,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using Process p = Process.Start(psi)!;
            p.WaitForExit(2000);
            return p.ExitCode is 0 or 1;
        }
        catch
        {
            return false;
        }
    }
}

