using Engine.Bundling.JavaScript.Models;

namespace Engine.Bundling.JavaScript;

public class JsModuleGraph
{
    private readonly Dictionary<string, ModuleNode> _nodes = [];
    private readonly Dictionary<string, ModuleInfo> _moduleInfos = [];

    public void AddModule(string filePath, ModuleInfo moduleInfo, IEnumerable<string> resolvedDependencies)
    {
        _moduleInfos[filePath] = moduleInfo;
        
        if (!_nodes.TryGetValue(filePath, out ModuleNode? node))
        {
            node = new ModuleNode
            {
                FilePath = filePath,
                Type = moduleInfo.Type,
                Info = moduleInfo
            };
            _nodes[filePath] = node;
        }
        else
        {
            node.Type = moduleInfo.Type;
            node.Info = moduleInfo;
        }

        node.Dependencies.Clear();
        foreach (string dependency in resolvedDependencies)
        {
            node.Dependencies.Add(dependency);

            if (!_nodes.TryGetValue(dependency, out ModuleNode? depNode))
            {
                depNode = new ModuleNode { FilePath = dependency };
                _nodes[dependency] = depNode;
            }
            depNode.Dependents.Add(filePath);
        }
    }

    public void MarkAsEntryPoint(string filePath)
    {
        if (!_nodes.TryGetValue(filePath, out ModuleNode? node))
            throw new InvalidOperationException($"Cannot mark non-existent module '{filePath}' as entry point");
        
        node.IsEntryPoint = true;
    }

    public List<List<string>> FindAllCircularDependencies()
    {
        List<List<string>> cycles = [];
        HashSet<string> visited = [];
        HashSet<string> recursionStack = [];
        List<string> currentPath = [];

        foreach (string module in _nodes.Keys)
        {
            if (!visited.Contains(module))
                FindCyclesRecursive(module, visited, recursionStack, currentPath, cycles);
        }

        return cycles;
    }

    private void FindCyclesRecursive(string module, HashSet<string> visited, HashSet<string> recursionStack, List<string> currentPath, List<List<string>> cycles)
    {
        visited.Add(module);
        recursionStack.Add(module);
        currentPath.Add(module);

        if (_nodes.TryGetValue(module, out ModuleNode? node))
        {
            foreach (string dependency in node.Dependencies)
            {
                if (!visited.Contains(dependency))
                {
                    FindCyclesRecursive(dependency, visited, recursionStack, currentPath, cycles);
                }
                else if (recursionStack.Contains(dependency))
                {
                    int startIndex = currentPath.IndexOf(dependency);
                    if (startIndex >= 0)
                    {
                        List<string> cycle = currentPath.GetRange(startIndex, currentPath.Count - startIndex);
                        cycle.Add(dependency);
                        cycles.Add(cycle);
                    }
                }
            }
        }

        currentPath.RemoveAt(currentPath.Count - 1);
        recursionStack.Remove(module);
    }
    
    public IEnumerable<string> GetEntryPoints()
    {
        return _nodes.Where(n => n.Value.IsEntryPoint).Select(n => n.Key);
    }
    
    public ModuleNode? GetModule(string filePath)
    {
        return _nodes.TryGetValue(filePath, out ModuleNode? node) ? node : null;
    }
    
    public ModuleInfo? GetModuleInfo(string filePath)
    {
        return _moduleInfos.TryGetValue(filePath, out ModuleInfo? info) ? info : null;
    }
    
    public List<CircularDependency> FindCircularDependencies()
    {
        List<List<string>> cycles = FindAllCircularDependencies();
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

    public static async Task<JsModuleGraph> BuildAsync(JsModuleResolver resolver, params string[] entryPoints)
    {
        JsModuleGraph graph = new();
        
        if (entryPoints.Length == 0)
            return graph;
        
        HashSet<string> processedFiles = [];
        
        List<Task> tasks = [];
        foreach (string entryPoint in entryPoints)
        {
            tasks.Add(ProcessModuleAsync(graph, resolver, processedFiles, entryPoint, isEntryPoint: true));
        }
        
        await Task.WhenAll(tasks);
        
        return graph;
    }

    private static async Task ProcessModuleAsync(JsModuleGraph graph, JsModuleResolver resolver, HashSet<string> processedFiles, string filePath, bool isEntryPoint = false)
    {
        if (!processedFiles.Add(filePath))
            return;
        
        string content = await File.ReadAllTextAsync(filePath);
        ModuleInfo moduleInfo = JsModuleParser.ParseModule(filePath, content);
        List<string> resolvedDependencies = [];
        List<Task> dependencyTasks = [];
        
        foreach (ImportStatement import in moduleInfo.Imports)
        {
            string? resolvedPath = resolver.ResolvePath(import.Source, filePath)
                ?? throw new InvalidOperationException($"Cannot resolve import '{import.Source}' from {filePath}");

            import.ResolvedPath = resolvedPath;
            resolvedDependencies.Add(resolvedPath);
            
            if (!resolvedPath.Contains(Folders.NodeModules) && !processedFiles.Contains(resolvedPath))
                dependencyTasks.Add(ProcessModuleAsync(graph, resolver, processedFiles, resolvedPath));
        }
        
        graph.AddModule(filePath, moduleInfo, resolvedDependencies);
        
        if (isEntryPoint)
            graph.MarkAsEntryPoint(filePath);
        
        await Task.WhenAll(dependencyTasks);
    }
}