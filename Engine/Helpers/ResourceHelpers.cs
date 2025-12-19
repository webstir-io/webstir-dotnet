using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Engine.Helpers;

public static class ResourceHelpers
{
    public static async Task CopyEmbeddedFileAsync(string resourceName, string outputPath)
        => await CopyEmbeddedFileAsync(resourceName, outputPath, overwriteExisting: true);

    public static async Task CopyEmbeddedFileAsync(string resourceName, string outputPath, bool overwriteExisting)
    {
        if (!overwriteExisting && File.Exists(outputPath))
        {
            return;
        }

        Assembly assembly = Assembly.GetExecutingAssembly();
        Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using Stream streamToCopy = stream;
        await using FileStream fileStream = File.Create(outputPath);
        await streamToCopy.CopyToAsync(fileStream).ConfigureAwait(false);
    }

    public static async Task CopyEmbeddedDirectoryAsync(string resourcePrefix, string destinationPath)
        => await CopyEmbeddedDirectoryAsync(resourcePrefix, destinationPath, overwriteExisting: true);

    public static async Task CopyEmbeddedDirectoryAsync(
        string resourcePrefix,
        string destinationPath,
        bool overwriteExisting)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourcePrefixWithDot = $"{resourcePrefix}.";
        string[] resources = [.. assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefixWithDot, StringComparison.Ordinal))];

        foreach (string resourceName in resources)
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                continue;
            }

            string resourcePath = resourceName.Replace(resourcePrefixWithDot, "");

            string relativePath = ConvertEmbeddedResourcePathToRelativePath(resourcePath);

            string outputPath = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            if (!overwriteExisting && File.Exists(outputPath))
            {
                continue;
            }

            using FileStream fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream);
        }
    }

    public static string[] ListEmbeddedDirectoryFiles(string resourcePrefix)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourcePrefixWithDot = $"{resourcePrefix}.";

        return [.. assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefixWithDot, StringComparison.Ordinal))
            .Select(name => name.Replace(resourcePrefixWithDot, ""))
            .Select(ConvertEmbeddedResourcePathToRelativePath)];
    }

    public static async Task CopyEmbeddedRootFilesAsync(string resourcePrefix, string destinationPath)
        => await CopyEmbeddedRootFilesAsync(resourcePrefix, destinationPath, overwriteExisting: true);

    public static async Task CopyEmbeddedRootFilesAsync(
        string resourcePrefix,
        string destinationPath,
        bool overwriteExisting)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string prefixWithDot = $"{resourcePrefix}.";

        string[] resources = [.. assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefixWithDot, StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}{Folders.Src}.", StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}Templates.", StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}features.", StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}modulehost.", StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}testhost.", StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}optional.", StringComparison.Ordinal))];

        foreach (string resourceName in resources)
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                continue;
            }

            string fileName = resourceName.Replace(prefixWithDot, "");
            string outputPath = Path.Combine(destinationPath, fileName);

            if (!overwriteExisting && File.Exists(outputPath))
            {
                continue;
            }

            using FileStream fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream);
        }
    }

    public static string[] ListEmbeddedRootFiles(string resourcePrefix)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string prefixWithDot = $"{resourcePrefix}.";

        return [.. assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefixWithDot, StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}{Folders.Src}.", StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}Templates.", StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}features.", StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}modulehost.", StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}testhost.", StringComparison.Ordinal)
                && !name.StartsWith($"{prefixWithDot}optional.", StringComparison.Ordinal))
            .Select(name => name.Replace(prefixWithDot, ""))];
    }

    private static string ConvertEmbeddedResourcePathToRelativePath(string resourcePath)
    {
        if (resourcePath.EndsWith(FileExtensions.Dts, StringComparison.Ordinal))
        {
            string basePart = resourcePath[..^FileExtensions.Dts.Length];
            return basePart.Replace('.', Path.DirectorySeparatorChar) + FileExtensions.Dts;
        }

        string testTs = Files.Test + FileExtensions.Ts;
        if (resourcePath.EndsWith(testTs, StringComparison.Ordinal))
        {
            string basePart = resourcePath[..^testTs.Length];
            return basePart.Replace('.', Path.DirectorySeparatorChar) + testTs;
        }

        int lastDotIndex = resourcePath.LastIndexOf('.');
        return lastDotIndex > 0
            ? resourcePath[..lastDotIndex].Replace('.', Path.DirectorySeparatorChar) + resourcePath[lastDotIndex..]
            : resourcePath.Replace('.', Path.DirectorySeparatorChar);
    }
}
