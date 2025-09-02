using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.JavaScript.Models;

namespace Engine.Pipelines.JavaScript.Publish;

public class JsBundler(AppWorkspace workspace)
{
    private readonly JsModuleResolver _resolver = new(workspace);
    
    public async Task BundleAsync(DiagnosticCollection? diagnostics = null) => await BundlePageScriptsAsync(diagnostics);
    
    private async Task BundlePageScriptsAsync(DiagnosticCollection? diagnostics)
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

            (string bundleCode, string sourceMap) = await BuildBundleAsync(pageScript, diagnostics);
            (string jsFileName, string mapFileName) = await WriteJsAsync(pageName, bundleCode, sourceMap);
            await UpdateJsManifestAsync(pageName, jsFileName, mapFileName);
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

    private async Task<(string BundleCode, string SourceMap)> BuildBundleAsync(string entryScriptPath, DiagnosticCollection? diagnostics)
    {
        JsModuleGraph graph = await JsModuleGraph.BuildAsync(_resolver, entryScriptPath);
        List<JsModuleInfo> modules = GetModulesInOrder(graph);

        if (diagnostics != null)
        {
            foreach (JsModuleInfo module in modules)
            {
                if (module.Type == JsModuleType.CommonJS)
                {
                    diagnostics.AddError(
                        "CommonJS detected; ESM required. Replace require()/module.exports with import/export.",
                        module.FilePath);
                }
            }
        }

        string bundleCode = ConcatenateModules(modules, graph);

        JsSourceMapGenerator mapGenerator = new();
        foreach (JsModuleInfo module in modules)
        {
            mapGenerator.AddMapping(module);
        }
        string sourceMap = mapGenerator.Generate();

        bundleCode = JsTransformer.Minify(bundleCode);

        return (bundleCode, sourceMap);
    }

    private async Task<(string JsFileName, string MapFileName)> WriteJsAsync(string pageName, string bundleCode, string sourceMap)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string jsFileName = $"{Files.Index}.{timestamp}{FileExtensions.Js}";
        string mapFileName = jsFileName + FileExtensions.Map;

        string pageDistDir = workspace.ClientDistPath.Combine(Folders.Pages, pageName);
        pageDistDir.Create();

        string bundleWithMapComment = bundleCode + "\n//# sourceMappingURL=" + mapFileName + "\n";
        string distJsPath = Path.Combine(pageDistDir, jsFileName);
        await File.WriteAllTextAsync(distJsPath, bundleWithMapComment);

        string distMapPath = Path.Combine(pageDistDir, mapFileName);
        await File.WriteAllTextAsync(distMapPath, sourceMap);

        return (jsFileName, mapFileName);
    }

    private Task UpdateJsManifestAsync(string pageName, string jsFileName, string mapFileName)
    {
        string pageDistDir = workspace.ClientDistPath.Combine(Folders.Pages, pageName);
        AssetManifest manifest = AssetManifest.Load(pageDistDir);
        manifest.Js = jsFileName;
        manifest.Map.Js = mapFileName;
        manifest.Save(pageDistDir);
        return Task.CompletedTask;
    }
}
