using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;

namespace Engine.Middleware;

public sealed class PrecompressionMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ShouldTryCompression(context, out string? path))
        {
            await _next(context);
            return;
        }

        string physicalPath = GetPhysicalPath(context, path!);
        CompressedFile? compressed = FindCompressedVariant(physicalPath, context.Request.Headers["Accept-Encoding"]);

        if (compressed == null)
        {
            await _next(context);
            return;
        }

        await ServeCompressedFile(context, physicalPath, compressed);
    }

    private static bool ShouldTryCompression(HttpContext context, out string? path)
    {
        path = context.Request.Path.Value;

        if (string.IsNullOrEmpty(path))
            return false;

        if (IsAlreadyCompressed(path))
            return false;

        string? acceptEncoding = context.Request.Headers["Accept-Encoding"].ToString();
        return !string.IsNullOrEmpty(acceptEncoding);
    }

    private static bool IsAlreadyCompressed(string path) =>
        path.EndsWith(FileExtensions.Br, StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(FileExtensions.Gz, StringComparison.OrdinalIgnoreCase);

    private static string GetPhysicalPath(HttpContext context, string requestPath)
    {
        IWebHostEnvironment env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        string relativePath = requestPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(env.WebRootPath, relativePath);
    }

    private static CompressedFile? FindCompressedVariant(string physicalPath, string? acceptEncoding)
    {
        if (string.IsNullOrEmpty(acceptEncoding))
            return null;

        // Check for Brotli support
        if (acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase))
        {
            string brPath = physicalPath + FileExtensions.Br;
            if (File.Exists(brPath))
                return new CompressedFile(brPath, "br");
        }

        if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
        {
            string gzipPath = physicalPath + FileExtensions.Gz;
            if (File.Exists(gzipPath))
                return new CompressedFile(gzipPath, "gzip");
        }

        return null;
    }

    private async Task ServeCompressedFile(HttpContext context, string originalPath, CompressedFile compressed)
    {
        SetResponseHeaders(context, originalPath, compressed.Encoding);
        await WriteFileToResponse(context, compressed.Path);
    }

    private void SetResponseHeaders(HttpContext context, string originalPath, string encoding)
    {
        string contentType = GetContentType(originalPath);

        context.Response.Headers.Vary = "Accept-Encoding";
        context.Response.Headers["Content-Encoding"] = encoding;
        context.Response.ContentType = contentType;
    }

    private static string GetContentType(string path)
    {
        return ContentTypes.TryGetContentType(path, out string? detected) && !string.IsNullOrEmpty(detected)
            ? detected
            : "application/octet-stream";
    }

    private static async Task WriteFileToResponse(HttpContext context, string filePath)
    {
        await using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true
        );

        await stream.CopyToAsync(context.Response.Body);
    }

    private sealed record CompressedFile(string Path, string Encoding);
}
