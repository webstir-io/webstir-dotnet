using Engine.Extensions;
using Engine.Pipelines.JavaScript.Models;

namespace Engine.Pipelines.JavaScript.Publish;

public class JsBundler(AppWorkspace workspace)
{
    private readonly JsModuleResolver _resolver = new(workspace);
    
    public async Task BundleAsync() => await BundlePageScriptsAsync();
    
    private async Task BundlePageScriptsAsync()
    {
        string pagesPath = workspace.ClientBuildPath.Combine(Folders.Pages);
        if (!pagesPath.Exists())
        {
            return;
        }
        
        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            string pageScript = pageDir.Combine($"{Files.Index}{FileExtensions.Js}");
            
            if (!pageScript.Exists())
            {
                continue;
            }
            
            JsModuleGraph graph = await JsModuleGraph.BuildAsync(_resolver, pageScript);
            List<JsModuleInfo> modules = GetModulesInOrder(graph);
            
            // Concatenate all transformed modules
            string bundleCode = ConcatenateModules(modules, graph);

            // Generate a coarse per-module source map (line-offset based)
            JsSourceMapGenerator mapGenerator = new();
            foreach (JsModuleInfo module in modules)
            {
                mapGenerator.AddMapping(module);
            }
            string sourceMap = mapGenerator.Generate();

            // Minify after concatenation; mapping remains coarse by design (v1)
            bundleCode = JsTransformer.Minify(bundleCode);
            
            string distPagePath = workspace.ClientDistPath.Combine(Folders.Pages, pageName, $"{Files.Index}{FileExtensions.Js}");
            distPagePath.DirectoryName().Create();
            await File.WriteAllTextAsync(distPagePath, bundleCode);

            // Emit separate source map file (no inline comment to keep dist clean)
            string distMapPath = distPagePath + FileExtensions.Map;
            await File.WriteAllTextAsync(distMapPath, sourceMap);
        }
    }

    private static List<JsModuleInfo> GetModulesInOrder(JsModuleGraph graph)
    {
        List<JsModuleInfo> result = [];
        HashSet<string> visited = [];
        
        foreach (string entryPoint in graph.GetEntryPoints())
        {
            VisitModule(entryPoint, graph, visited, result);
        }

        return result;
    }

    private static void VisitModule(string modulePath, JsModuleGraph graph, HashSet<string> visited, List<JsModuleInfo> result)
    {
        if (!visited.Add(modulePath))
        {
            return;
        }
        
        JsModuleNode? node = graph.GetModule(modulePath);
        if (node?.Info == null)
        {
            return;
        }
        
        foreach (string dependency in node.Dependencies)
        {
            VisitModule(dependency, graph, visited, result);
        }
        
        result.Add(node.Info);
    }

    private static string ConcatenateModules(List<JsModuleInfo> modules, JsModuleGraph graph)
    {
        Dictionary<string, int> moduleIdMap = [];
        for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            moduleIdMap[modules[moduleIndex].FilePath] = moduleIndex;
        }
        
        List<string> transformedModules = [];
        HashSet<string> usedExports = JsTreeShaker.AnalyzeUsage(graph);
        
        for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            JsTransformedModule transformed = JsTransformer.Transform(modules[moduleIndex], moduleIndex, moduleIdMap);
            transformed = new JsTransformedModule
            {
                Id = transformed.Id,
                Code = JsTransformer.RemoveUnusedCode(transformed.Code, modules[moduleIndex], usedExports),
                SourceMap = transformed.SourceMap
            };
            
            transformedModules.Add(transformed.Code);
        }

        return string.Join("\n\n", transformedModules);
    }
}
