using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Engine.Helpers;

public static class AssemblyHelpers
{
    public static void WriteResourceToFile(string resourcePath, string filename, string filepath)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string[] resourceNames = assembly.GetManifestResourceNames();
        string fullResourceName = $"Engine.Resources.{resourcePath}.{filename}";
        string resourceName = resourceNames.SingleOrDefault(p => p == fullResourceName)
            ?? throw new Exception($"Embedded resource '{fullResourceName}' not found");

        using Stream resource = assembly.GetManifestResourceStream(resourceName)!;
        using FileStream file = new(filepath, FileMode.Create, FileAccess.Write);
        resource.Seek(0, SeekOrigin.Begin);
        resource.CopyTo(file);
    }

    public static void WriteResourceToFile(string filename, string filepath)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string[] resourceNames = assembly.GetManifestResourceNames();
        string resourceName = resourceNames.SingleOrDefault(p => p.Contains(filename, StringComparison.Ordinal))
            ?? throw new Exception($"Embedded resources '{filename}' not found");

        using Stream resource = assembly.GetManifestResourceStream(resourceName)!;
        using FileStream file = new(filepath, FileMode.Create, FileAccess.Write);
        resource.Seek(0, SeekOrigin.Begin);
        resource.CopyTo(file);
    }
}
