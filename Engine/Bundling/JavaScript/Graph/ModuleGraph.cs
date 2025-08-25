namespace Engine.Bundling.JavaScript.Graph;

public class ModuleGraph
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
}