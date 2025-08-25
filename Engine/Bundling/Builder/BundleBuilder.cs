using Engine.Bundling.Graph;
using Engine.Bundling.Transform;
using Engine.Bundling.SourceMaps;

namespace Engine.Bundling.Builder;

public class BundleBuilder(
    ModuleGraphBuilder graphBuilder,
    ModuleTransformer transformer,
    TreeShaker treeShaker,
    SourceMapGenerator sourceMapGenerator,
    BundleOptions options)
{
    public async Task<BundleResult> BuildBundleAsync(params string[] entryPoints)
    {
        ModuleGraph graph = await graphBuilder.BuildGraphAsync(entryPoints);
        
        if (options.EnableTreeShaking)
            treeShaker.AnalyzeUsage(graph);
        
        List<ModuleInfo> modules = GetModulesInOrder(graph);
        
        string bundleCode = ConcatenateModules(modules);
        string? sourceMap = options.GenerateSourceMap ? GenerateSourceMap(modules) : null;
        
        return new BundleResult
        {
            Code = bundleCode,
            SourceMap = sourceMap,
            ModulePaths = [.. modules.Select(m => m.FilePath)]
        };
    }

    private static List<ModuleInfo> GetModulesInOrder(ModuleGraph graph)
    {
        List<ModuleInfo> result = [];
        HashSet<string> visited = [];
        
        foreach (string entryPoint in graph.GetEntryPoints())
            VisitModule(entryPoint, graph, visited, result);
        
        return result;
    }

    private static void VisitModule(string modulePath, ModuleGraph graph, HashSet<string> visited, List<ModuleInfo> result)
    {
        if (!visited.Add(modulePath))
            return;
        
        ModuleNode? node = graph.GetModule(modulePath);
        if (node?.Info == null)
            return;
        
        foreach (string dependency in node.Dependencies)
            VisitModule(dependency, graph, visited, result);
        
        result.Add(node.Info);
    }

    private string ConcatenateModules(List<ModuleInfo> modules)
    {
        Dictionary<string, int> moduleIdMap = [];
        for (int i = 0; i < modules.Count; i++)
            moduleIdMap[modules[i].FilePath] = i;
        
        List<string> transformedModules = [];
        
        for (int i = 0; i < modules.Count; i++)
        {
            TransformedModule transformed = transformer.Transform(modules[i], i, moduleIdMap);
            transformedModules.Add(transformed.Code);
        }
        
        return string.Join("\n\n", transformedModules);
    }

    private string GenerateSourceMap(List<ModuleInfo> modules)
    {
        sourceMapGenerator.Clear();
        
        foreach (ModuleInfo module in modules)
            sourceMapGenerator.AddMapping(module);
        
        return sourceMapGenerator.Generate();
    }
}