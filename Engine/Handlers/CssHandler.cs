using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;
using Engine.Processors.Css;

namespace Engine.Handlers;

public class CssHandler(AppContext context) : IHandler
{
    private const string _appCssFile = "app.css";
    private const string _indexCssFile = "index.css";
    private const string _resetCssFile = "reset.css";
    private const string _baseCssFile = "base.css";

    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack)
    {
        string stylesPath = context.ClientAppPath.CreateSubDirectory(Folders.Styles);

        string appCssFilepath = context.ClientAppPath.Combine(_appCssFile);
        if (!File.Exists(appCssFilepath))
            AssemblyHelpers.WriteResourceToFile(Resources.ClientResourcesPath, _appCssFile, appCssFilepath);

        string resetCssFilepath = stylesPath.Combine(_resetCssFile);
        if (!File.Exists(resetCssFilepath))
            AssemblyHelpers.WriteResourceToFile(Resources.ClientResourcesPath, _resetCssFile, resetCssFilepath);

        string baseCssFilepath = stylesPath.Combine(_baseCssFile);
        if (!File.Exists(baseCssFilepath))
            AssemblyHelpers.WriteResourceToFile(Resources.ClientResourcesPath, _baseCssFile, baseCssFilepath);

        string homeFilepath = context.ClientPagesPath.CreateSubDirectory(Folders.Home);
        string indexCssOutputFilepath = homeFilepath.Combine(_indexCssFile);
        if (!File.Exists(indexCssOutputFilepath))
            AssemblyHelpers.WriteResourceToFile(Resources.ClientResourcesPath, _indexCssFile, indexCssOutputFilepath);

        await Task.CompletedTask;
    }

    public async Task BuildAsync(bool releaseMode = false)
    {
        string contextCssFilepath = context.ClientAppPath.Combine(_appCssFile);
        if (File.Exists(contextCssFilepath))
        {
            string contextCssContent = File.ReadAllText(contextCssFilepath);
            if (CssImportProcessor.HasImportStatements(contextCssContent))
            {
                BuildWithImports();
                return;
            }
        }

        await Task.CompletedTask;
    }

    private void BuildWithImports()
    {
        foreach (string page in context.ClientPagesPath.Folders())
        {
            string? pageCssFile = page.Files(AddCssExt(page.Name())).FirstOrDefault();
            if (pageCssFile == null)
                continue;

            string cssContent = File.ReadAllText(pageCssFile);
            string outputFilepath = context.ClientBuildPath
                    .CreateSubDirectory(page)
                    .Combine(AddCssExt(page.Name()));

            cssContent = CssImportProcessor.ProcessForBuild(cssContent, pageCssFile, outputFilepath, context.ClientPath);

            File.WriteAllText(outputFilepath, cssContent);
        }
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