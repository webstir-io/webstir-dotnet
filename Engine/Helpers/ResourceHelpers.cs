using System.Reflection;

namespace Engine.Helpers;

public static class ResourceHelpers
{
    public static void CopyEmbeddedDirectory(string resourcePrefix, string destinationPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith($"CLI.Resources.{resourcePrefix}."))
            .ToList();

        foreach (var resourceName in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;
            
            // Extract the relative path by removing the prefix
            var relativePath = resourceName
                .Replace($"CLI.Resources.{resourcePrefix}.", "")
                .Replace('.', Path.DirectorySeparatorChar);
            
            var parts = relativePath.Split(Path.DirectorySeparatorChar);
            if (parts.Length > 1)
            {
                var fileName = parts[^1];
                var extensionIndex = fileName.LastIndexOf('_');
                if (extensionIndex > 0)
                {
                    parts[^1] = string.Concat(fileName.AsSpan(0, extensionIndex), ".", fileName.AsSpan(extensionIndex + 1));
                }
                relativePath = Path.Combine(parts);
            }
            
            var outputPath = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            
            using var fileStream = File.Create(outputPath);
            stream.CopyTo(fileStream);
        }
    }
}