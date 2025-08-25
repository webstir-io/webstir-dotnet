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
            await ProcessBuildFileAsync(srcFile);
    }

    private async Task ProcessBuildFileAsync(string srcFile)
    {
        string cssContent = File.ReadAllText(srcFile);
        string relativePath = Path.GetRelativePath(workspace.ClientPath, srcFile);
        string buildPath = workspace.ClientBuildPath.Combine(relativePath);
        
        Path.GetDirectoryName(buildPath)!.Create();
        
        string processedContent = CssImportProcessor.ProcessForBuild(
            cssContent, 
            srcFile, 
            buildPath, 
            workspace.ClientPath
        );

        File.WriteAllText(buildPath, processedContent);
        
        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        bool usesImports = CheckForImports();
        string[] allCssFiles = GetNonTimestampedCssFiles();
        
        foreach (string srcFile in allCssFiles)
            await ProcessPublishFileAsync(srcFile, usesImports);
    }

    private bool CheckForImports()
    {
        var contextCssFilepath = workspace.ClientAppPath.Combine(_appCssFile);
        if (!File.Exists(contextCssFilepath))
            return false;
            
        var contextCssContent = File.ReadAllText(contextCssFilepath);
        return CssImportProcessor.HasImportStatements(contextCssContent);
    }

    private string[] GetNonTimestampedCssFiles()
    {
        return workspace.ClientBuildPath.Files(AddCssExt("*"), SearchOption.AllDirectories);
    }

    private async Task ProcessPublishFileAsync(string srcFile, bool usesImports)
    {
        string relativePath = Path.GetRelativePath(workspace.ClientBuildPath, srcFile);
        string cssContent = GetPublishContent(srcFile, relativePath, usesImports);
        string distPath = workspace.ClientDistPath.Combine(relativePath);
        
        Path.GetDirectoryName(distPath)!.Create();
        
        string minifiedContent = CssMinifier.Minify(cssContent);
        File.WriteAllText(distPath, minifiedContent);
        
        await Task.CompletedTask;
    }

    private string GetPublishContent(string srcFile, string relativePath, bool usesImports)
    {
        string cssContent = File.ReadAllText(srcFile);
        
        if (!usesImports)
            return cssContent;
            
        var sourceFilePath = workspace.ClientPath.Combine(relativePath);
        if (!File.Exists(sourceFilePath))
            return cssContent;
            
        cssContent = File.ReadAllText(sourceFilePath);
        return CssImportProcessor.ProcessForPublish(cssContent, sourceFilePath, workspace.ClientPath);
    }



    public async Task AddPageAsync(string pageName)
    {
        var cssContent = $"/* {pageName} Page Styles */\n@import \"@app/app.css\";\n\n/* Add your page-specific styles here */\n";
        var pageDirectory = workspace.ClientPagesPath.Combine(pageName);
        var cssFilePath = pageDirectory.Combine(AddCssExt(Files.Index));
        File.WriteAllText(cssFilePath, cssContent);
        await Task.CompletedTask;
    }

    private static string AddCssExt(string value)
    {
        return $"{value}.css";
    }
}