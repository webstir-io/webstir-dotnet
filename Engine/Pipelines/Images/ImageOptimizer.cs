using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Engine.Pipelines.Images;

public static class ImageOptimizer
{
    public static async Task OptimizeAsync(string sourceRoot, string destRoot)
    {
        ArgumentNullException.ThrowIfNull(sourceRoot);
        ArgumentNullException.ThrowIfNull(destRoot);

        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        string[] files = Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories);
        foreach (string srcFile in files)
        {
            string ext = Path.GetExtension(srcFile).ToLowerInvariant();
            if (!IsImageExtension(ext))
            {
                continue;
            }

            string relative = Path.GetRelativePath(sourceRoot, srcFile);
            string destPath = Path.Combine(destRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            if (string.Equals(ext, FileExtensions.Svg, StringComparison.OrdinalIgnoreCase))
            {
                string svg = await File.ReadAllTextAsync(srcFile);
                string sanitized = SvgSanitizer.Sanitize(svg);
                await File.WriteAllTextAsync(destPath, sanitized);
            }
            else
            {
                File.Copy(srcFile, destPath, true);
                await TryCreateWebPAsync(destPath);
                await TryCreateAvifAsync(destPath);
            }
        }
    }

    public static bool TryGetImageDimensions(string filePath, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!File.Exists(filePath))
        {
            return false;
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        try
        {
            byte[] buffer = File.ReadAllBytes(filePath);
            return ext switch
            {
                FileExtensions.Png => TryGetPngDimensions(buffer, out width, out height),
                FileExtensions.Jpg or FileExtensions.Jpeg => TryGetJpegDimensions(buffer, out width, out height),
                FileExtensions.Webp => TryGetWebpDimensions(buffer, out width, out height),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetPngDimensions(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;
        // PNG signature (8 bytes) + IHDR chunk (length 4, type 4) then width/height 8 bytes
        if (data.Length < 24)
        {
            return false;
        }
        // Verify PNG signature
        byte[] sig = [137, 80, 78, 71, 13, 10, 26, 10];
        for (int i = 0; i < sig.Length; i++)
        {
            if (data[i] != sig[i])
            {
                return false;
            }
        }
        // Width/height at bytes 16..23 (big-endian)
        width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
        height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
        return width > 0 && height > 0;
    }

    private static bool TryGetJpegDimensions(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return false;
        }

        int index = 2;
        while (index + 9 < data.Length)
        {
            if (data[index] != 0xFF)
            {
                index++;
                continue;
            }
            byte marker = data[index + 1];
            index += 2;

            // Skip padding FFs
            while (index < data.Length && data[index] == 0xFF)
            {
                index++;
            }
            if (index + 1 >= data.Length)
            {
                break;
            }
            int length = (data[index] << 8) + data[index + 1];
            if (length <= 2 || index + length > data.Length)
            {
                break;
            }

            // SOF0 (0xC0), SOF2 (0xC2) contain dimensions
            if (marker is 0xC0 or 0xC2)
            {
                if (index + 7 < data.Length)
                {
                    height = (data[index + 3] << 8) + data[index + 4];
                    width = (data[index + 5] << 8) + data[index + 6];
                    return width > 0 && height > 0;
                }
                break;
            }

            index += length;
        }

        return false;
    }

    private static bool TryGetWebpDimensions(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (data.Length < 16)
        {
            return false;
        }
        // RIFF....WEBP
        if (!(data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
            && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P'))
        {
            return false;
        }

        int index = 12;
        while (index + 8 <= data.Length)
        {
            // Chunk header: 4cc + size (little-endian)
            uint fourcc = (uint)(data[index] | (data[index + 1] << 8) | (data[index + 2] << 16) | (data[index + 3] << 24));
            int size = data[index + 4] | (data[index + 5] << 8) | (data[index + 6] << 16) | (data[index + 7] << 24);
            index += 8;
            if (index + size > data.Length)
            {
                break;
            }

            // 'VP8X' extended header stores canvas size (width-1, height-1) as 24-bit little-endian values
            if (fourcc == 0x58385056) // 'VP8X'
            {
                if (size >= 10)
                {
                    // Skip 1 byte flags + 3 reserved
                    int w = data[index + 4] | (data[index + 5] << 8) | (data[index + 6] << 16);
                    int h = data[index + 7] | (data[index + 8] << 8) | (data[index + 9] << 16);
                    width = w + 1;
                    height = h + 1;
                    return width > 0 && height > 0;
                }
            }
            else if (fourcc == 0x20385056) // 'VP8 '
            {
                // Lossy: frame header at 10 bytes from start of chunk: 3 bytes signature, 7 bytes, then 2 bytes width/height little-endian?
                // Use minimal parse per spec for practical purposes is complex; skip here for simplicity
            }
            else if (fourcc == 0x4C385056) // 'VP8L'
            {
                // Lossless: first 5 bytes of chunk data contain VP8L signature and dimensions
                if (size >= 5)
                {
                    byte b0 = data[index + 1];
                    byte b1 = data[index + 2];
                    byte b2 = data[index + 3];
                    byte b3 = data[index + 4];
                    int w = 1 + (((b1 & 0x3F) << 8) | b0);
                    int h = 1 + (((b3 & 0x0F) << 10) | (b2 << 2) | ((b1 & 0xC0) >> 6));
                    width = w;
                    height = h;
                    return width > 0 && height > 0;
                }
            }

            // Chunks are padded to even sizes
            index += size + (size % 2);
        }

        return false;
    }

    private static bool IsImageExtension(string ext)
    {
        return string.Equals(ext, FileExtensions.Png, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, FileExtensions.Jpg, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, FileExtensions.Jpeg, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, FileExtensions.Gif, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, FileExtensions.Svg, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, FileExtensions.Webp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, FileExtensions.Ico, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task TryCreateWebPAsync(string sourcePath)
    {
        string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (string.Equals(ext, FileExtensions.Webp, StringComparison.OrdinalIgnoreCase))
        {
            return; // already webp
        }

        if (!IsToolAvailable("cwebp"))
        {
            return;
        }

        string destPath = Path.ChangeExtension(sourcePath, FileExtensions.Webp);
        // cwebp input -q 75 -o output
        await RunProcessAsync("cwebp", $"-quiet -q 75 \"{sourcePath}\" -o \"{destPath}\"");
    }

    private static async Task TryCreateAvifAsync(string sourcePath)
    {
        string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (string.Equals(ext, ".avif", StringComparison.OrdinalIgnoreCase))
        {
            return; // already avif
        }

        if (!IsToolAvailable("avifenc"))
        {
            return;
        }

        string destPath = Path.ChangeExtension(sourcePath, ".avif");
        // avifenc --min 20 --max 30 input output
        await RunProcessAsync("avifenc", $"--quiet --min 20 --max 30 \"{sourcePath}\" \"{destPath}\"");
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
            return p.ExitCode is 0 or 1; // some tools return 1 for --version
        }
        catch
        {
            return false;
        }
    }

    private static async Task RunProcessAsync(string fileName, string arguments)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process p = Process.Start(psi)!;
            await p.WaitForExitAsync();
        }
        catch
        {
            // ignore failures; fallback is original image
        }
    }
}
