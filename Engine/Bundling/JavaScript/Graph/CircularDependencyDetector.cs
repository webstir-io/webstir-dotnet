namespace Engine.Bundling.JavaScript.Graph;

public class CircularDependencyDetector(ModuleGraph graph)
{
    public List<CircularDependency> FindCircularDependencies()
    {
        List<List<string>> cycles = graph.FindAllCircularDependencies();
        List<CircularDependency> result = [];

        foreach (List<string> cycle in cycles)
        {
            result.Add(new CircularDependency
            {
                Modules = cycle,
                Path = BuildCyclePath(cycle)
            });
        }

        return result;
    }

    private static string BuildCyclePath(List<string> cycle)
    {
        if (cycle.Count == 0)
            return string.Empty;

        List<string> relativePaths = [.. cycle.Select(GetRelativePath)];
        return string.Join(" → ", relativePaths);
    }

    private static string GetRelativePath(string fullPath)
    {
        string srcFolder = Path.DirectorySeparatorChar + Folders.Src + Path.DirectorySeparatorChar;
        int srcIndex = fullPath.IndexOf(srcFolder, StringComparison.OrdinalIgnoreCase);
        
        if (srcIndex >= 0)
            return fullPath[(srcIndex + 1)..];
        
        return Path.GetFileName(fullPath);
    }
}