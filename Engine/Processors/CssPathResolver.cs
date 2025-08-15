namespace Engine.Processors;

/// <summary>
/// Handles path resolution for CSS files, including namespace mappings and relative paths
/// </summary>
public static class CssPathResolver
{
    // Namespace mapping for @import and url() resolution
    private static readonly Dictionary<string, string> NamespaceMap = new()
    {
        { "@app/", "app/" },
        { "@components/", "app/components/" },
        { "@shared/", "shared/styles/" },
        { "@pages/", "pages/" }
    };
    
    /// <summary>
    /// Resolves a path considering namespace mappings and relative paths
    /// </summary>
    public static string ResolvePath(string path, string baseDirectory, string clientDirectory)
    {
        // Handle namespace paths
        foreach (var ns in NamespaceMap)
        {
            if (path.StartsWith(ns.Key))
            {
                var relativePath = path[ns.Key.Length..];
                var clientPath = Path.Combine(clientDirectory, ns.Value, relativePath);
                return Path.GetFullPath(clientPath);
            }
        }
        
        // Handle relative paths
        if (path.StartsWith("./") || path.StartsWith("../"))
        {
            var fullPath = Path.Combine(baseDirectory, path);
            return Path.GetFullPath(fullPath);
        }
        
        // Handle absolute paths from client root
        var absolutePath = Path.Combine(clientDirectory, path);
        return Path.GetFullPath(absolutePath);
    }
    
    /// <summary>
    /// Gets a relative path from one directory to another
    /// </summary>
    public static string GetRelativePath(string fromPath, string toPath)
    {
        // Ensure paths are absolute
        fromPath = Path.GetFullPath(fromPath);
        toPath = Path.GetFullPath(toPath);
        
        // Use Path.GetRelativePath for .NET Core 2.0+
        var relativePath = Path.GetRelativePath(fromPath, toPath);
        
        // Ensure forward slashes for CSS
        return relativePath.Replace('\\', '/');
    }
    
    /// <summary>
    /// Checks if a path is external (http://, https://, data:, etc.)
    /// </summary>
    public static bool IsExternalPath(string path)
    {
        return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
               path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || 
               path.StartsWith("//") || 
               path.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Checks if a path uses namespace syntax
    /// </summary>
    public static bool IsNamespacePath(string path)
    {
        foreach (var ns in NamespaceMap.Keys)
        {
            if (path.StartsWith(ns))
                return true;
        }
        return false;
    }
}