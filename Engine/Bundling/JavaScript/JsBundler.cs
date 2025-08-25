using Engine.Extensions;
using Engine.Bundling.JavaScript.Models;

namespace Engine.Bundling.JavaScript;

public class JsBundler(AppWorkspace workspace)
{
    private const string AppJsFile = "app.js";
    private readonly JsModuleResolver _resolver = new(workspace);
    
    public async Task BundleAsync()
    {
        string entryPoint = workspace.ClientAppPath.Combine(AppJsFile);
        
        if (!File.Exists(entryPoint))
            throw new FileNotFoundException($"JavaScript entry point not found: {entryPoint}");

        JsModuleGraph graph = await JsModuleGraph.BuildAsync(_resolver, entryPoint);
        
        List<ModuleInfo> modules = GetModulesInOrder(graph);
        
        string bundleCode = ConcatenateModules(modules, graph);
        bundleCode = JsTransformer.Minify(bundleCode);
        
        string distPath = workspace.ClientDistPath.Combine(AppJsFile);
        Path.GetDirectoryName(distPath)!.Create();
        await File.WriteAllTextAsync(distPath, bundleCode);
    }

    private static List<ModuleInfo> GetModulesInOrder(JsModuleGraph graph)
    {
        List<ModuleInfo> result = [];
        HashSet<string> visited = [];
        
        foreach (string entryPoint in graph.GetEntryPoints())
            VisitModule(entryPoint, graph, visited, result);
        
        return result;
    }

    private static void VisitModule(string modulePath, JsModuleGraph graph, HashSet<string> visited, List<ModuleInfo> result)
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

    private string ConcatenateModules(List<ModuleInfo> modules, JsModuleGraph graph)
    {
        Dictionary<string, int> moduleIdMap = [];
        for (int i = 0; i < modules.Count; i++)
            moduleIdMap[modules[i].FilePath] = i;
        
        List<string> transformedModules = [];
        HashSet<string> usedExports = JsTreeShaker.AnalyzeUsage(graph);
        
        for (int i = 0; i < modules.Count; i++)
        {
            TransformedModule transformed = JsTransformer.Transform(modules[i], i, moduleIdMap);
            transformed = new TransformedModule
            {
                Id = transformed.Id,
                Code = JsTransformer.RemoveUnusedCode(transformed.Code, modules[i], usedExports),
                SourceMap = transformed.SourceMap
            };
            
            transformedModules.Add(transformed.Code);
        }
        
        return string.Join("\n\n", transformedModules);
    }

}