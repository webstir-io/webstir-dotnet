using System.Reflection;

namespace Engine.Helpers;

public static class AssemblyHelpers
{
    public static void WriteResourceToFile(string resourcePath, string filename, string filepath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var fullResourceName = $"Engine.Templates.{resourcePath}.{filename}";
        var resourceName = resourceNames.SingleOrDefault(p => p == fullResourceName) 
            ?? throw new Exception($"Embedded resource '{fullResourceName}' not found");
        
        using var resource = assembly.GetManifestResourceStream(resourceName)!;
        using var file = new FileStream(filepath, FileMode.Create, FileAccess.Write);
        resource.Seek(0, SeekOrigin.Begin);
        resource.CopyTo(file);
    }
    
    public static void WriteResourceToFile(string filename, string filepath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var resourceName = resourceNames.SingleOrDefault(p => p.Contains(filename)) 
            ?? throw new Exception($"Embedded resources '{filename}' not found");
        
        using var resource = assembly.GetManifestResourceStream(resourceName)!;
        using var file = new FileStream(filepath, FileMode.Create, FileAccess.Write);
        resource.Seek(0, SeekOrigin.Begin);
        resource.CopyTo(file);
    }
}