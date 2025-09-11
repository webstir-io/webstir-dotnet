using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Engine.Pipelines.Core;

public static class Precompression
{
    public static async Task CreatePrecompressedVariantsAsync(string sourceFilePath)
    {
        ArgumentNullException.ThrowIfNull(sourceFilePath);

        if (!File.Exists(sourceFilePath))
        {
            return;
        }

        await CreateBrotliAsync(sourceFilePath);
        await CreateGzipAsync(sourceFilePath);
    }

    private static async Task CreateBrotliAsync(string sourceFilePath)
    {
        string targetPath = sourceFilePath + FileExtensions.Br;

        await using FileStream input = new(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        await using FileStream output = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);

        // CompressionLevel.SmallestSize corresponds to Brotli quality 11
        using BrotliStream brotli = new(output, CompressionLevel.SmallestSize, leaveOpen: true);
        await input.CopyToAsync(brotli);
        await brotli.FlushAsync();
    }

    private static async Task CreateGzipAsync(string sourceFilePath)
    {
        string targetPath = sourceFilePath + FileExtensions.Gz;

        await using (FileStream input = new(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true))
        await using (FileStream output = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
        {
            // Use the smallest size (roughly level 9) for publish-time precompression
            using GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true);
            await input.CopyToAsync(gzip);
            await gzip.FlushAsync();
            await output.FlushAsync();
        }

        // Ensure deterministic gzip header: zero the MTIME field (bytes 4..7)
        // This makes gzip output reproducible across builds - same input always produces
        // identical compressed output, regardless of when compression occurs.
        // The gzip format includes a modification timestamp that would otherwise vary.
        await using FileStream fixup = new(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 8192, useAsync: true);
        if (fixup.Length >= 10)
        {
            byte[] header = new byte[10];
            int read = await fixup.ReadAsync(header, 0, header.Length);
            if (read == header.Length && header[0] == 0x1F && header[1] == 0x8B && header[2] == 0x08)
            {
                // Zero MTIME (bytes 4..7)
                fixup.Seek(4, SeekOrigin.Begin);
                await fixup.WriteAsync(new byte[] { 0, 0, 0, 0 });
            }
        }
    }
}
