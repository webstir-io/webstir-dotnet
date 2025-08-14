using System.Reflection;

namespace Engine.Helpers;

public static class ResourceHelpers
{
    public static async Task CopyEmbeddedDirectoryAsync(string resourcePrefix, string destinationPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith($"{resourcePrefix}."))
            .ToArray();

        foreach (var resourceName in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;
            
            var resourcePath = resourceName.Replace($"{resourcePrefix}.", "");
            var lastDotIndex = resourcePath.LastIndexOf('.');
            
            var relativePath = lastDotIndex > 0 
                ? resourcePath[..lastDotIndex].Replace('.', Path.DirectorySeparatorChar) + resourcePath[lastDotIndex..]
                : resourcePath.Replace('.', Path.DirectorySeparatorChar);
                
            var outputPath = Path.Combine(destinationPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            
            using var fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream);
        }
    }

    public static async Task CopyEmbeddedRootFilesAsync(string resourcePrefix, string destinationPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var prefixWithDot = $"{resourcePrefix}.";
        
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefixWithDot) && 
                          !name.StartsWith($"{prefixWithDot}src."))
            .ToArray();

        foreach (var resourceName in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;
            
            var fileName = resourceName.Replace(prefixWithDot, "");
            var outputPath = Path.Combine(destinationPath, fileName);
            
            using var fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream);
        }
    }
}