using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Engine.Pipelines.Core.Utilities;

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
    }

    private static async Task CreateBrotliAsync(string sourceFilePath)
    {
        string targetPath = sourceFilePath + FileExtensions.Br;

        await using FileStream input = new(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        await using FileStream output = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        using BrotliStream brotli = new(output, CompressionLevel.SmallestSize, leaveOpen: true);
        await input.CopyToAsync(brotli);
        await brotli.FlushAsync();
    }
}
