namespace Engine.Pipelines.Css.Build;

public static class CssPathResolver
{
    private static readonly Dictionary<string, string> NamespaceMap = new()
    {
        { "@app/", "app/" },
        { "@components/", "app/components/" },
        { "@shared/", "shared/styles/" },
        { "@pages/", "pages/" }
    };
    
    public static string ResolvePath(string path, string baseDirectory, string clientDirectory)
    {
        foreach (KeyValuePair<string, string> ns in NamespaceMap)
        {
            if (path.StartsWith(ns.Key))
            {
                string relativePath = path[ns.Key.Length..];
                string clientPath = Path.Combine(clientDirectory, ns.Value, relativePath);
                return Path.GetFullPath(clientPath);
            }
        }
        
        if (path.StartsWith("./") || path.StartsWith("../"))
        {
            string fullPath = Path.Combine(baseDirectory, path);
            return Path.GetFullPath(fullPath);
        }
        
        string absolutePath = Path.Combine(clientDirectory, path);
        return Path.GetFullPath(absolutePath);
    }
}