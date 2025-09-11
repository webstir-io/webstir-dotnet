using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using CssConstants = Engine.Pipelines.Css.Common.Css;
using Engine.Pipelines.Css.Common;
using Engine.Pipelines.Css.Models;
using Engine.Pipelines.Css.Parsing;
using Engine.Pipelines.Css.Transformation;

namespace Engine.Pipelines.Css.Bundling;

public class CssBundler(AppWorkspace workspace)
{
    private readonly CssModuleGraph _graph = new();

    public async Task BundleAsync() => await BundlePageStylesAsync();

    private async Task BundlePageStylesAsync()
    {
        string pagesPath = workspace.FrontendBuildPath.Combine(Folders.Pages);
        if (!pagesPath.Exists())
        {
            return;
        }

        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            // Prefer CSS Modules if present; otherwise fall back to plain CSS
            string moduleStylePath = pageDir.Combine($"{Files.Index}{CssConstants.ModuleExt}");
            string plainStylePath = pageDir.Combine($"{Files.Index}{FileExtensions.Css}");

            string? entryStylePath = moduleStylePath.Exists()
                ? moduleStylePath
                : (plainStylePath.Exists() ? plainStylePath : null);

            if (string.IsNullOrEmpty(entryStylePath))
            {
                continue;
            }

            string finalCss = await BuildBundledCssAsync(entryStylePath);
            string cssFileName = await WriteCssAsync(pageName, finalCss);
            await UpdateCssManifestAsync(pageName, cssFileName);
        }
    }

    private async Task<string> BuildBundledCssAsync(string entryModulePath)
    {
        _graph.Clear();

        await LoadModuleRecursively(entryModulePath);
        List<CssModule> modules = _graph.GetModulesInOrder([entryModulePath]);

        StringBuilder bundled = new();
        foreach (CssModule module in modules)
        {
            string processed = await ProcessModule(module);
            bundled.AppendLine(processed);
        }

        string finalCss = bundled.ToString();
        finalCss = Transformer.AddPrefixes(finalCss);
        finalCss = Transformer.StripLegacyPrefixes(finalCss);
        finalCss = Transformer.Minify(finalCss);
        return finalCss;
    }

    private async Task<string> WriteCssAsync(string pageName, string finalCss)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string cssFileName = $"{Files.Index}.{timestamp}{FileExtensions.Css}";
        string pageDistDir = workspace.FrontendDistPath.Combine(Folders.Pages, pageName);
        pageDistDir.Create();

        string distCssPath = Path.Combine(pageDistDir, cssFileName);
        await File.WriteAllTextAsync(distCssPath, finalCss);

        // Create precompressed variants for transport (Brotli and gzip)
        await Engine.Pipelines.Core.Precompression.CreatePrecompressedVariantsAsync(distCssPath);

        return cssFileName;
    }

    private Task UpdateCssManifestAsync(string pageName, string cssFileName)
    {
        string pageDistDir = workspace.FrontendDistPath.Combine(Folders.Pages, pageName);
        AssetManifest.Update(pageDistDir, m => m.Css = cssFileName);
        return Task.CompletedTask;
    }

    private async Task<CssModule> LoadModuleRecursively(string filePath)
    {
        CssModule? existing = _graph.GetModule(filePath);
        if (existing != null)
        {
            return existing;
        }

        if (!filePath.Exists())
        {
            throw new FileNotFoundException($"CSS file not found: {filePath}");
        }

        string content = await File.ReadAllTextAsync(filePath);
        string directory = filePath.DirectoryName();

        List<CssImport> imports = CssParser.ExtractImports(content, directory);
        content = CssRegex.Import().Replace(content, string.Empty);

        Dictionary<string, string> classMappings = [];
        if (filePath.EndsWith(CssConstants.ModuleExt, StringComparison.OrdinalIgnoreCase))
        {
            CssProcessedModule processed = Transformer.ProcessModule(content, filePath);
            content = processed.Content;
            classMappings = processed.ClassMappings;
        }

        CssModule module = new()
        {
            FilePath = filePath,
            Content = content,
            Imports = imports,
            ClassMappings = classMappings,
            Hash = CssModuleGraph.GenerateHash(content),
            LastModified = File.GetLastWriteTime(filePath)
        };

        _graph.AddModule(module);

        foreach (CssImport import in imports)
        {
            await LoadModuleRecursively(import.ResolvedPath);
        }

        return module;
    }

    private static Task<string> ProcessModule(CssModule module)
    {
        string content = module.Content;

        // Remove non-important comments only; preserve /*! ... */ license comments
        content = CssRegex.NonImportantBlockComment().Replace(content, string.Empty);

        List<string> urls = CssParser.ExtractUrls(content);
        if (urls.Count > 0)
        {
            string baseDir = module.FilePath.DirectoryName();
            content = CssParser.UpdateUrls(content, url => ResolveUrl(url, baseDir));
        }

        if (module.Imports.Any(importItem => importItem.Media != null))
        {
            StringBuilder wrapped = new();
            foreach (CssImport import in module.Imports.Where(importItem => importItem.Media != null))
            {
                wrapped.AppendLine(FormattableString.Invariant($"@media {import.Media} {{"));
            }
            wrapped.Append(content);
            foreach (CssImport import in module.Imports.Where(importItem => importItem.Media != null))
            {
                wrapped.AppendLine("}");
            }

            content = wrapped.ToString();
        }

        return Task.FromResult(content);
    }

    private static string ResolveUrl(string url, string baseDirectory)
    {
        if (url.StartsWith("http://", StringComparison.Ordinal) || url.StartsWith("https://", StringComparison.Ordinal) || url.StartsWith("data:", StringComparison.Ordinal))
        {
            return url;
        }

        if (url.StartsWith('/'))
        {
            return url;
        }

        string resolved = Path.GetRelativePath(Directory.GetCurrentDirectory(),
            Path.GetFullPath(baseDirectory.Combine(url)));

        return resolved.Replace('\\', '/');
    }
}
