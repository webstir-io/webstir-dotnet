using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Utilities;
using Engine.Pipelines.JavaScript.Minification;
using Engine.Pipelines.JavaScript.Models;
using Engine.Pipelines.JavaScript.Transformation;

namespace Engine.Pipelines.JavaScript.Bundling;

public class JsBundler(AppWorkspace workspace)
{
    private readonly JsModuleResolver _resolver = new(workspace);

    public async Task BundleAsync(DiagnosticCollection? diagnostics = null) => await BundlePageScriptsAsync(diagnostics);

    private async Task BundlePageScriptsAsync(DiagnosticCollection? diagnostics)
    {
        string pagesPath = workspace.FrontendBuildPath.Combine(Folders.Pages);
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

            string bundleCode = await BuildBundleAsync(pageScript, diagnostics);
            string jsFileName = await WriteJsAsync(pageName, bundleCode);
            await UpdateJsManifestAsync(pageName, jsFileName);
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
            JsTransformedModule transformed = JsModuleTransformer.Transform(modules[moduleIndex], moduleIndex, moduleIdMap);
            transformed = new JsTransformedModule
            {
                Id = transformed.Id,
                Code = JsTreeShaker.RemoveUnusedCode(transformed.Code, modules[moduleIndex], usedExports),
                SourceMap = transformed.SourceMap
            };

            transformedModules.Add(transformed.Code);
        }

        return string.Join("\n\n", transformedModules);
    }

    private async Task<string> BuildBundleAsync(string entryScriptPath, DiagnosticCollection? diagnostics)
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
        bundleCode = JsMinifier.Minify(bundleCode);

        return bundleCode;
    }

    private async Task<string> WriteJsAsync(string pageName, string bundleCode)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string jsFileName = $"{Files.Index}.{timestamp}{FileExtensions.Js}";

        string pageDistDir = workspace.FrontendDistPath.Combine(Folders.Pages, pageName);
        pageDistDir.Create();

        string distJsPath = Path.Combine(pageDistDir, jsFileName);
        await File.WriteAllTextAsync(distJsPath, bundleCode);
        await Precompression.CreatePrecompressedVariantsAsync(distJsPath);

        return jsFileName;
    }

    private Task UpdateJsManifestAsync(string pageName, string jsFileName)
    {
        string pageDistDir = workspace.FrontendDistPath.Combine(Folders.Pages, pageName);
        AssetManifest.Update(pageDistDir, m => m.Js = jsFileName);

        return Task.CompletedTask;
    }
}
