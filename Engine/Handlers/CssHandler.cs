using Engine.Extensions;
using Engine.Processors;

namespace Engine.Handlers;

public class CssHandler(AppWorkspace workspace)
{
    private const string _appCssFile = "app.css";

    public async Task BuildAsync()
    {
        string[] allCssFiles = workspace.ClientPath.Files(AddCssExt("*"), SearchOption.AllDirectories);
        
        foreach (string srcFile in allCssFiles)
        {
            string cssContent = File.ReadAllText(srcFile);
            string relativePath = Path.GetRelativePath(workspace.ClientPath, srcFile);
            string buildFilePath = workspace.ClientBuildPath.Combine(relativePath);
            Path.GetDirectoryName(buildFilePath)!.Create();

            cssContent = CssImportProcessor.ProcessForBuild(cssContent, srcFile, buildFilePath, workspace.ClientPath);
            File.WriteAllText(buildFilePath, cssContent);
        }

        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        var contextCssFilepath = workspace.ClientAppPath.Combine(_appCssFile);
        var usesImports = false;
        if (File.Exists(contextCssFilepath))
        {
            var contextCssContent = File.ReadAllText(contextCssFilepath);
            usesImports = CssImportProcessor.HasImportStatements(contextCssContent);
        }

        string[] allCssFiles = workspace.ClientBuildPath.Files(AddCssExt("*"), SearchOption.AllDirectories);
        
        foreach (string srcFile in allCssFiles)
        {
            string cssContent = File.ReadAllText(srcFile);
            string relativePath = Path.GetRelativePath(workspace.ClientBuildPath, srcFile);
            string distFilePath = workspace.ClientDistPath.Combine(relativePath);
            Path.GetDirectoryName(distFilePath)!.Create();

            if (usesImports)
            {
                var sourceFilePath = workspace.ClientPath.Combine(relativePath);
                if (File.Exists(sourceFilePath))
                {
                    cssContent = File.ReadAllText(sourceFilePath);
                    cssContent = CssImportProcessor.ProcessForPublish(cssContent, sourceFilePath, workspace.ClientPath);
                }
            }

            cssContent = CssMinifier.Minify(cssContent);
            File.WriteAllText(distFilePath, cssContent);
        }

        await Task.CompletedTask;
    }

    public async Task AddPageAsync(string pageName)
    {
        var cssContent = $"/* {pageName} Page Styles */\n@import \"@app/app.css\";\n\n/* Add your page-specific styles here */\n";
        var pageDirectory = workspace.ClientPagesPath.Combine(pageName);
        var cssFilePath = pageDirectory.Combine(AddCssExt(Files.Index));
        File.WriteAllText(cssFilePath, cssContent);
        await Task.CompletedTask;
    }
    
    private static string AddCssExt(string pageName)
    {
        return $"{Files.Index}.css";
    }
}