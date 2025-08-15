using Engine.Extensions;
using Engine.Processors;

namespace Engine.Handlers;

public class CssHandler(AppContext context)
{
    private const string _appCssFile = "app.css";

    public async Task BuildAsync()
    {
        string[] allCssFiles = context.ClientPath.Files(AddCssExt("*"), SearchOption.AllDirectories);
        
        foreach (string srcFile in allCssFiles)
        {
            string cssContent = File.ReadAllText(srcFile);
            string relativePath = Path.GetRelativePath(context.ClientPath, srcFile);
            string buildFilePath = context.ClientBuildPath.Combine(relativePath);
            Path.GetDirectoryName(buildFilePath)!.Create();

            cssContent = CssImportProcessor.ProcessForBuild(cssContent, srcFile, buildFilePath, context.ClientPath);
            File.WriteAllText(buildFilePath, cssContent);
        }

        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        // Check if the project uses @import statements
        var contextCssFilepath = context.ClientAppPath.Combine(_appCssFile);
        var usesImports = false;
        if (File.Exists(contextCssFilepath))
        {
            var contextCssContent = File.ReadAllText(contextCssFilepath);
            usesImports = CssImportProcessor.HasImportStatements(contextCssContent);
        }

        // Process all CSS files from build directory, preserving structure
        string[] allCssFiles = context.ClientBuildPath.Files(AddCssExt("*"), SearchOption.AllDirectories);
        
        foreach (string srcFile in allCssFiles)
        {
            string cssContent = File.ReadAllText(srcFile);
            string relativePath = Path.GetRelativePath(context.ClientBuildPath, srcFile);
            string distFilePath = context.ClientDistPath.Combine(relativePath);
            Path.GetDirectoryName(distFilePath)!.Create();

            if (usesImports)
            {
                // Process imports for publish mode (inline all imports)
                var sourceFilePath = context.ClientPath.Combine(relativePath);
                if (File.Exists(sourceFilePath))
                {
                    cssContent = File.ReadAllText(sourceFilePath);
                    cssContent = CssImportProcessor.ProcessForPublish(cssContent, sourceFilePath, context.ClientPath);
                }
            }

            cssContent = CssMinifier.Minify(cssContent);
            File.WriteAllText(distFilePath, cssContent);
        }

        await Task.CompletedTask;
    }

    public async Task AddPageAsync(string pageName)
    {
        var cssContent = $"/* {pageName} Page Styles */\n@import \"@context/context.css\";\n\n/* Add your page-specific styles here */\n";
        var pageDirectory = context.ClientPagesPath.Combine(pageName);
        var cssFilePath = pageDirectory.Combine(AddCssExt(Files.Index));
        File.WriteAllText(cssFilePath, cssContent);
        await Task.CompletedTask;
    }
    
    private static string AddCssExt(string pageName)
    {
        return $"{pageName}.css";
    }
}