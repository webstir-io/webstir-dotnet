using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Engine.Pipelines.JavaScript.Models;

namespace Engine.Pipelines.JavaScript.Publish;

public sealed class JsModuleGraph
{
    private readonly Dictionary<string, JsModuleNode> _nodes = [];
    private readonly Dictionary<string, JsModuleInfo> _moduleInfos = [];

    public void AddModule(string filePath, JsModuleInfo moduleInfo, IEnumerable<string> resolvedDependencies)
    {
        ArgumentNullException.ThrowIfNull(moduleInfo);
        ArgumentNullException.ThrowIfNull(resolvedDependencies);
        _moduleInfos[filePath] = moduleInfo;

        if (!_nodes.TryGetValue(filePath, out JsModuleNode? node))
        {
            node = new JsModuleNode
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

            if (!_nodes.TryGetValue(dependency, out JsModuleNode? dependencyNode))
            {
                dependencyNode = new JsModuleNode { FilePath = dependency };
                _nodes[dependency] = dependencyNode;
            }
            dependencyNode.Dependents.Add(filePath);
        }
    }

    public void MarkAsEntryPoint(string filePath)
    {
        if (!_nodes.TryGetValue(filePath, out JsModuleNode? node))
        {
            // Be tolerant: ensure an entry node exists to avoid failing the publish
            node = new JsModuleNode { FilePath = filePath };
            _nodes[filePath] = node;
        }
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
            {
                FindCyclesRecursive(module, visited, recursionStack, currentPath, cycles);
            }
        }

        return cycles;
    }

    private void FindCyclesRecursive(string module, HashSet<string> visited, HashSet<string> recursionStack, List<string> currentPath, List<List<string>> cycles)
    {
        visited.Add(module);
        recursionStack.Add(module);
        currentPath.Add(module);

        if (_nodes.TryGetValue(module, out JsModuleNode? node))
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

    public IEnumerable<string> GetEntryPoints() => _nodes.Where(n => n.Value != null && n.Value.IsEntryPoint).Select(n => n.Key);

    public JsModuleNode? GetModule(string filePath) => _nodes.TryGetValue(filePath, out JsModuleNode? node) ? node : null;

    public JsModuleInfo? GetModuleInfo(string filePath) => _moduleInfos.TryGetValue(filePath, out JsModuleInfo? info) ? info : null;

    public List<JsCircularDependency> FindCircularDependencies()
    {
        List<List<string>> cycles = FindAllCircularDependencies();
        List<JsCircularDependency> result = [];

        foreach (List<string> cycle in cycles)
        {
            result.Add(new JsCircularDependency
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
        {
            return string.Empty;
        }

        List<string> relativePaths = [.. cycle.Select(GetRelativePath)];
        return string.Join(" → ", relativePaths);
    }

    private static string GetRelativePath(string fullPath)
    {
        string srcFolder = Path.DirectorySeparatorChar + Folders.Src + Path.DirectorySeparatorChar;
        int srcIndex = fullPath.IndexOf(srcFolder, StringComparison.OrdinalIgnoreCase);

        if (srcIndex >= 0)
        {
            return fullPath[(srcIndex + 1)..];
        }

        return Path.GetFileName(fullPath);
    }

    public static async Task<JsModuleGraph> BuildAsync(JsModuleResolver resolver, params string[] entryPoints)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(entryPoints);
        JsModuleGraph graph = new();

        if (entryPoints.Length == 0)
        {
            return graph;
        }

        HashSet<string> processedFiles = [];

        // Process entry points sequentially to avoid concurrent mutations
        foreach (string entryPoint in entryPoints)
        {
            await ProcessModuleAsync(graph, resolver, processedFiles, entryPoint, isEntryPoint: true);
        }

        return graph;
    }

    private static async Task ProcessModuleAsync(JsModuleGraph graph, JsModuleResolver resolver, HashSet<string> processedFiles, string filePath, bool isEntryPoint = false)
    {
        if (!processedFiles.Add(filePath))
        {
            return;
        }

        string content = await File.ReadAllTextAsync(filePath);
        JsModuleInfo moduleInfo = JsModuleParser.ParseModule(filePath, content);
        List<string> resolvedDependencies = [];
        // Process dependencies depth-first to avoid concurrent mutations

        foreach (JsImportStatement import in moduleInfo.Imports)
        {
            string? resolvedPath = resolver.ResolvePath(import.Source, filePath)
                ?? throw new InvalidOperationException($"Cannot resolve import '{import.Source}' from {filePath}");

            import.ResolvedPath = resolvedPath;
            resolvedDependencies.Add(resolvedPath);

            if (!resolvedPath.Contains(Folders.NodeModules, StringComparison.Ordinal) && !processedFiles.Contains(resolvedPath))
            {
                await ProcessModuleAsync(graph, resolver, processedFiles, resolvedPath);
            }
        }

        graph.AddModule(filePath, moduleInfo, resolvedDependencies);

        if (isEntryPoint)
        {
            graph.MarkAsEntryPoint(filePath);
        }

        // No outstanding tasks due to sequential processing
    }
}
