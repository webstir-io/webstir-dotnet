using Engine.Extensions;

namespace Engine.Pipelines.Css.Build;

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
        
        buildPath.DirectoryName().Create();
        
        string processedContent = CssImportProcessor.ProcessForBuild(
            cssContent,
            srcFile,
            buildPath,
            workspace.ClientPath,
            workspace.ClientBuildPath
        );

        File.WriteAllText(buildPath, processedContent);
    }
}
