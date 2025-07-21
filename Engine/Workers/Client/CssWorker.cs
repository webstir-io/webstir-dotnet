using Engine.Extensions;
using Engine.Helpers;
using Engine.Servers;
using Engine.Models;
using Engine.Processors.Css;

namespace Engine.Workers.Client;

public class StylesWorker(App app) : IClientWorker
{
    private const string _appCssFile = "app.css";
    private const string _indexCssFile = "index.css";
    private const string _resetCssFile = "reset.css";
    private const string _baseCssFile = "base.css";

    public int BuildOrder => 3; // Fast operations can run together after TS compilation

    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        var stylesDirectory = app.ClientAppDir.CreateSubdirectory(App.Folders.Styles);

        var appCssFilepath = stylesDirectory.CombinePath(_appCssFile);
        if (!File.Exists(appCssFilepath))
            AssemblyHelpers.WriteResourceToFile("client", _appCssFile, appCssFilepath);

        var resetCssFilepath = stylesDirectory.CombinePath(_resetCssFile);
        if (!File.Exists(resetCssFilepath))
            AssemblyHelpers.WriteResourceToFile("client.styles", _resetCssFile, resetCssFilepath);

        var baseCssFilepath = stylesDirectory.CombinePath(_baseCssFile);
        if (!File.Exists(baseCssFilepath))
            AssemblyHelpers.WriteResourceToFile("client.styles", _baseCssFile, baseCssFilepath);

        var indexCssOutputFilepath = app.ClientIndexDir.CombinePath(_indexCssFile);
        if (!File.Exists(indexCssOutputFilepath))
            AssemblyHelpers.WriteResourceToFile("client", _indexCssFile, indexCssOutputFilepath);
    }

    public void Build(bool releaseMode = false)
    {
        var appCssFilepath = app.ClientAppDir.CombinePath(_appCssFile);
        if (File.Exists(appCssFilepath))
        {
            var appCssContent = File.ReadAllText(appCssFilepath);
            if (CssImportProcessor.HasImportStatements(appCssContent))
            {
                BuildWithImports();
                return;
            }
        }    
    }
    
    private void BuildWithImports()
    {
        // Process each page directory
        foreach (var pageDirectory in app.ClientPagesDir.GetDirectories())
        {
            var pageCssFile = pageDirectory.GetFiles($"{pageDirectory.Name}.css").FirstOrDefault();
            if (pageCssFile == null)
                continue;
                
            // Read the page CSS content
            var cssContent = File.ReadAllText(pageCssFile.FullName);
            
            // Determine output path
            string outputFilepath;
            if (pageDirectory.Name.Equals("index", StringComparison.OrdinalIgnoreCase))
            {
                outputFilepath = app.ClientBuildDir.CombinePath($"{pageDirectory.Name}.css");
            }
            else
            {
                outputFilepath = app.ClientBuildDir
                    .CreateSubDirectory(pageDirectory.Name)
                    .CombinePath($"{pageDirectory.Name}.css");
            }
            
            // Process imports for build mode
            cssContent = CssImportProcessor.ProcessForBuild(cssContent, pageCssFile.FullName, outputFilepath, app.ClientDir.FullName);
            
            // Write the processed CSS
            File.WriteAllText(outputFilepath, cssContent);
        }
    }

    public void Publish()
    {
        // Check if the project uses @import statements
        var appCssFilepath = app.ClientAppDir.CombinePath(_appCssFile);
        var usesImports = false;
        if (File.Exists(appCssFilepath))
        {
            var appCssContent = File.ReadAllText(appCssFilepath);
            usesImports = CssImportProcessor.HasImportStatements(appCssContent);
        }
        
        // Process root index.css if it exists
        var rootIndexCss = app.ClientBuildDir.GetFiles("index.css").FirstOrDefault();
        if (rootIndexCss != null)
        {
            var cssContent = File.ReadAllText(rootIndexCss.FullName);
            
            if (usesImports)
            {
                // Process imports for publish mode (inline all imports)
                var sourcePagePath = app.ClientPagesDir.CombinePath(App.Folders.Index, _indexCssFile);
                if (File.Exists(sourcePagePath))
                {
                    cssContent = File.ReadAllText(sourcePagePath);
                    cssContent = CssImportProcessor.ProcessForPublish(cssContent, sourcePagePath, app.ClientDir.FullName);
                }
            }
            
            cssContent = CssMinifier.Minify(cssContent);
            var targetPath = app.ClientDistDir.CombinePath(rootIndexCss.Name);
            File.WriteAllText(targetPath, cssContent);
        }
        
        // Process CSS files in page directories
        foreach (var pageDirectory in app.ClientBuildDir.GetDirectories())
        {
            // Skip non-page directories
            if (pageDirectory.Name == "app" || pageDirectory.Name == "images")
                continue;

            var distPageDirectory = app.ClientDistDir.CreateSubDirectory(pageDirectory.Name);

            foreach (var cssFile in pageDirectory.GetFiles("*.css"))
            {
                var cssContent = File.ReadAllText(cssFile.FullName);
                
                if (usesImports)
                {
                    // Process imports for publish mode (inline all imports)
                    var sourcePagePath = app.ClientPagesDir.CombinePath(pageDirectory.Name, cssFile.Name);
                    if (File.Exists(sourcePagePath))
                    {
                        cssContent = File.ReadAllText(sourcePagePath);
                        cssContent = CssImportProcessor.ProcessForPublish(cssContent, sourcePagePath, app.ClientDir.FullName);
                    }
                }
                
                cssContent = CssMinifier.Minify(cssContent);

                var targetPath = distPageDirectory.CombinePath(cssFile.Name);
                File.WriteAllText(targetPath, cssContent);
            }
        }
    }

    public void AddPage(DirectoryInfo pageDirectory)
    {
        var pageName = pageDirectory.Name;
        var cssContent = $"/* {pageName} Page Styles */\n@import \"@app/app.css\";\n\n/* Add your page-specific styles here */\n";
        File.WriteAllText(pageDirectory.CombinePath($"{pageName}.css"), cssContent);
    }

    public void AddPage(string pageName)
    {
        var pageDirectory = app.ClientPagesDir.CreateSubDirectory(pageName);
        AddPage(pageDirectory);
    }

}