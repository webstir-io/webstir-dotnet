using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;
using Engine.Processors.Css;

namespace Engine.Workers.Client;

public class StylesWorker(AppContext context) : IClientWorker
{
    private const string _contextCssFile = "context.css";
    private const string _homeCssFile = "home.css";
    private const string _resetCssFile = "reset.css";
    private const string _baseCssFile = "base.css";

    public int BuildOrder => 3;

    public async Task Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        string stylesPath = context.ClientAppPath.CreateSubDirectory(Folders.Styles);

        string contextCssFilepath = stylesPath.Combine(_contextCssFile);
        if (!File.Exists(contextCssFilepath))
            AssemblyHelpers.WriteResourceToFile(Folders.Client, _contextCssFile, contextCssFilepath);

        string resetCssFilepath = stylesPath.Combine(_resetCssFile);
        if (!File.Exists(resetCssFilepath))
            AssemblyHelpers.WriteResourceToFile($"{Folders.Client}.{Folders.Styles}", _resetCssFile, resetCssFilepath);

        string baseCssFilepath = stylesPath.Combine(_baseCssFile);
        if (!File.Exists(baseCssFilepath))
            AssemblyHelpers.WriteResourceToFile($"{Folders.Client}.{Folders.Styles}", _baseCssFile, baseCssFilepath);

        string homeCssOutputFilepath = context.ClientPagesPath.CreateSubDirectory(_homeCssFile);
        if (!File.Exists(homeCssOutputFilepath))
            AssemblyHelpers.WriteResourceToFile(Folders.Client, _homeCssFile, homeCssOutputFilepath);

        await Task.CompletedTask;
    }

    public async Task Build(bool releaseMode = false)
    {
        string contextCssFilepath = context.ClientAppPath.Combine(_contextCssFile);
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

    public async Task Publish()
    {
        // Check if the project uses @import statements
        var contextCssFilepath = context.ClientAppPath.Combine(_contextCssFile);
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

    public async Task AddPage(string pageName)
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