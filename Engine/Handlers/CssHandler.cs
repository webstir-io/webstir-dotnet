using Engine.Extensions;
using Engine.Processors;

namespace Engine.Handlers;

public class CssHandler(AppContext context)
{
    private const string _appCssFile = "app.css";

    public async Task BuildAsync(bool releaseMode = false)
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


        // Process CSS files in page directories
        foreach (var page in context.ClientBuildPath.Folders())
        {
            if (page.Name() == "context" || page.Name() == "images")
                continue;

            var distPageDirectory = context.ClientDistPath.CreateSubDirectory(page.Name());

            foreach (var cssFile in page.Files(AddCssExt("*")))
            {
                var cssContent = File.ReadAllText(cssFile);

                if (usesImports)
                {
                    // Process imports for publish mode (inline all imports)
                    var sourcePagePath = context.ClientPagesPath.Combine(page.Name(), cssFile.Name());
                    if (File.Exists(sourcePagePath))
                    {
                        cssContent = File.ReadAllText(sourcePagePath);
                        cssContent = CssImportProcessor.ProcessForPublish(cssContent, sourcePagePath, context.ClientPath);
                    }
                }

                cssContent = CssMinifier.Minify(cssContent);

                var targetPath = distPageDirectory.Combine(cssFile.Name());
                File.WriteAllText(targetPath, cssContent);
            }
        }

        await Task.CompletedTask;
    }

    public async Task AddPageAsync(string pageName)
    {
        var cssContent = $"/* {pageName} Page Styles */\n@import \"@context/context.css\";\n\n/* Add your page-specific styles here */\n";
        File.WriteAllText(context.ClientPagesPath.Combine(AddCssExt(pageName)), cssContent);
        await Task.CompletedTask;
    }
    
    private static string AddCssExt(string pageName)
    {
        return $"{pageName}.css";
    }
}