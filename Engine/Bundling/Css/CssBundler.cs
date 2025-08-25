using Engine.Bundling.Css.Models;
using Engine.Extensions;
using System.Text;

namespace Engine.Bundling.Css;

public class CssBundler(AppWorkspace workspace)
{
    private readonly CssModuleGraph _graph = new();
    private const string AppCssFile = "app.css";

    public async Task BundleAsync()
    {
        string entryPoint = workspace.ClientAppPath.Combine(AppCssFile);
        
        if (!File.Exists(entryPoint))
            throw new FileNotFoundException($"CSS entry point not found: {entryPoint}");

        await LoadModuleRecursively(entryPoint);

        List<CssModule> modules = _graph.GetModulesInOrder([entryPoint]);
        
        StringBuilder bundled = new();
        Dictionary<string, Dictionary<string, string>> allMappings = new();

        foreach (CssModule module in modules)
        {
            string processed = await ProcessModule(module);
            bundled.AppendLine(processed);

            if (module.ClassMappings.Count > 0)
                allMappings[module.FilePath] = module.ClassMappings;
        }

        string finalCss = bundled.ToString();
        finalCss = CssTransformer.AddPrefixes(finalCss);
        finalCss = CssTransformer.Minify(finalCss);

        string distPath = workspace.ClientDistPath.Combine(AppCssFile);
        Path.GetDirectoryName(distPath)!.Create();
        await File.WriteAllTextAsync(distPath, finalCss);
        
        if (allMappings.Count > 0)
        {
            foreach ((string modulePath, Dictionary<string, string> mappings) in allMappings)
            {
                string jsExport = GenerateCssModuleExport(mappings);
                string jsPath = modulePath.Replace(CssConstants.ModuleExtension, $"{CssConstants.ModuleExtension}.js");
                string relativePath = Path.GetRelativePath(workspace.ClientPath, jsPath);
                string distJsPath = workspace.ClientDistPath.Combine(relativePath);
                
                Path.GetDirectoryName(distJsPath)!.Create();
                await File.WriteAllTextAsync(distJsPath, jsExport);
            }
        }
    }

    private static string GenerateCssModuleExport(Dictionary<string, string> mappings)
    {
        if (mappings.Count == 0)
            return "export default {};";

        List<string> exports = [];
        foreach ((string original, string scoped) in mappings)
            exports.Add($"  {original}: '{scoped}'");

        return $"export default {{\n{string.Join(",\n", exports)}\n}};";
    }

    private async Task<CssModule> LoadModuleRecursively(string filePath)
    {
        CssModule? existing = _graph.GetModule(filePath);
        if (existing != null)
            return existing;

        if (!filePath.Exists())
            throw new FileNotFoundException($"CSS file not found: {filePath}");

        string content = await File.ReadAllTextAsync(filePath);
        string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        
        List<CssImport> imports = CssParser.ExtractImports(content, directory);
        content = CssRegex.Import().Replace(content, string.Empty);

        Dictionary<string, string> classMappings = new();
        if (filePath.EndsWith(CssConstants.ModuleExtension))
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

    private Task<string> ProcessModule(CssModule module)
    {
        string content = module.Content;

        content = CssRegex.BlockComment().Replace(content, string.Empty);

        List<string> urls = CssParser.ExtractUrls(content);
        if (urls.Count > 0)
        {
            string baseDir = Path.GetDirectoryName(module.FilePath) ?? string.Empty;
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
            Path.GetFullPath(Path.Combine(baseDirectory, url)));
        
        return resolved.Replace('\\', '/');
    }
}