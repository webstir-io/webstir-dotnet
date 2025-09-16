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
        await CreateGzipAsync(sourceFilePath);
    }

    private static async Task CreateBrotliAsync(string sourceFilePath)
    {
        await CreateCompressedAssetAsync(
            sourceFilePath,
            FileExtensions.Br,
            stream => new BrotliStream(stream, CompressionLevel.SmallestSize, leaveOpen: true));
    }

    private static async Task CreateGzipAsync(string sourceFilePath)
    {
        await CreateCompressedAssetAsync(
            sourceFilePath,
            FileExtensions.Gz,
            stream => new GZipStream(stream, CompressionLevel.SmallestSize, leaveOpen: true));
    }

    private static async Task CreateCompressedAssetAsync(
        string sourceFilePath,
        string extension,
        Func<Stream, Stream> compressorFactory)
    {
        ArgumentNullException.ThrowIfNull(sourceFilePath);
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentNullException.ThrowIfNull(compressorFactory);

        string targetPath = sourceFilePath + extension;

        await using FileStream input = new(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        await using FileStream output = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        using Stream compressor = compressorFactory(output);
        await input.CopyToAsync(compressor);
        await compressor.FlushAsync();
    }
}
