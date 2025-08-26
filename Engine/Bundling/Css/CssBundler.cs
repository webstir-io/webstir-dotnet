using Engine.Bundling.Css.Models;
using Engine.Extensions;
using System.Text;

namespace Engine.Bundling.Css;

public class CssBundler(AppWorkspace workspace)
{
    private readonly CssModuleGraph _graph = new();

    public async Task BundleAsync()
    {
        await BundlePageStylesAsync();
    }
    
    private async Task BundlePageStylesAsync()
    {
        string pagesPath = workspace.ClientBuildPath.Combine(Folders.Pages);
        if (!pagesPath.Exists())
            return;
        
        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            string pageStyle = pageDir.Combine($"{Files.Index}{CssConstants.CssExtension}");
            
            if (!pageStyle.Exists())
                continue;
            
            _graph.Clear();
            
            await LoadModuleRecursively(pageStyle);
            List<CssModule> modules = _graph.GetModulesInOrder([pageStyle]);
            
            StringBuilder bundled = new();
            foreach (CssModule module in modules)
            {
                string processed = await ProcessModule(module);
                bundled.AppendLine(processed);
            }
            
            string finalCss = bundled.ToString();
            finalCss = CssTransformer.AddPrefixes(finalCss);
            finalCss = CssTransformer.Minify(finalCss);
            
            string distPagePath = workspace.ClientDistPath.Combine(Folders.Pages, pageName, $"{Files.Index}{CssConstants.CssExtension}");
            distPagePath.DirectoryName().Create();
            await File.WriteAllTextAsync(distPagePath, finalCss);
        }
    }


    private async Task<CssModule> LoadModuleRecursively(string filePath)
    {
        CssModule? existing = _graph.GetModule(filePath);
        if (existing != null)
            return existing;

        if (!filePath.Exists())
            throw new FileNotFoundException($"CSS file not found: {filePath}");

        string content = await File.ReadAllTextAsync(filePath);
        string directory = filePath.DirectoryName();
        
        List<CssImport> imports = CssParser.ExtractImports(content, directory);
        content = CssRegex.Import().Replace(content, string.Empty);

        Dictionary<string, string> classMappings = [];
        if (filePath.EndsWith(CssConstants.ModuleExtension, StringComparison.OrdinalIgnoreCase))
        {
            ProcessedCssModule processed = CssTransformer.ProcessModule(content, filePath);
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
            await LoadModuleRecursively(import.ResolvedPath);

        return module;
    }

    private static Task<string> ProcessModule(CssModule module)
    {
        string content = module.Content;

        content = CssRegex.BlockComment().Replace(content, string.Empty);

        List<string> urls = CssParser.ExtractUrls(content);
        if (urls.Count > 0)
        {
            string baseDir = module.FilePath.DirectoryName();
            content = CssParser.UpdateUrls(content, url => ResolveUrl(url, baseDir));
        }

        if (module.Imports.Any(i => i.Media != null))
        {
            StringBuilder wrapped = new();
            foreach (CssImport import in module.Imports.Where(i => i.Media != null))
                wrapped.AppendLine($"@media {import.Media} {{");
            wrapped.Append(content);
            foreach (CssImport import in module.Imports.Where(i => i.Media != null))
                wrapped.AppendLine("}");

            content = wrapped.ToString();
        }

        return Task.FromResult(content);
    }

    private static string ResolveUrl(string url, string baseDirectory)
    {
        if (url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("data:"))
            return url;

        if (url.StartsWith('/'))
            return url;

        string resolved = Path.GetRelativePath(Directory.GetCurrentDirectory(), 
            Path.GetFullPath(baseDirectory.Combine(url)));
        
        return resolved.Replace('\\', '/');
    }
}