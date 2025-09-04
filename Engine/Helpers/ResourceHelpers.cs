using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Engine.Helpers;

public static class ResourceHelpers
{
    public static async Task CopyEmbeddedDirectoryAsync(string resourcePrefix, string destinationPath)
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
            int lastDotIndex = resourcePath.LastIndexOf('.');

            string relativePath = lastDotIndex > 0
                ? resourcePath[..lastDotIndex].Replace('.', Path.DirectorySeparatorChar) + resourcePath[lastDotIndex..]
                : resourcePath.Replace('.', Path.DirectorySeparatorChar);

            string outputPath = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using FileStream fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream);
        }
    }

    public static async Task CopyEmbeddedRootFilesAsync(string resourcePrefix, string destinationPath)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string prefixWithDot = $"{resourcePrefix}.";

        string[] resources = [.. assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefixWithDot, StringComparison.Ordinal) && !name.StartsWith($"{prefixWithDot}{Folders.Src}.", StringComparison.Ordinal))];

        foreach (string resourceName in resources)
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                continue;
            }

            string fileName = resourceName.Replace(prefixWithDot, "");
            string outputPath = Path.Combine(destinationPath, fileName);

            using FileStream fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream);
        }
    }
}
