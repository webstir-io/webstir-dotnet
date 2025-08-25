using Engine.Extensions;

namespace Engine.Building.Css;

public class CssBuilder(AppWorkspace workspace)
{
    public void Build()
    {
        string[] cssFiles = workspace.ClientPath.Files("*.css", SearchOption.AllDirectories);

        foreach (string srcFile in cssFiles)
            ProcessBuildFile(srcFile);
    }

    private void ProcessBuildFile(string srcFile)
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
    }
}