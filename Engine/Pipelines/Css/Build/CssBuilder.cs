using System.IO;
using Engine.Extensions;
using Engine.Pipelines.Core;

namespace Engine.Pipelines.Css.Build;

public class CssBuilder(AppWorkspace workspace)
{
    public void Build(DiagnosticCollection? diagnostics = null)
    {
        string[] cssFiles = workspace.FrontendPath.Files("*.css", SearchOption.AllDirectories);

        foreach (string srcFile in cssFiles)
        {
            ProcessBuildFile(srcFile, diagnostics);
        }
    }

    private void ProcessBuildFile(string srcFile, DiagnosticCollection? diagnostics)
    {
        string cssContent = File.ReadAllText(srcFile);
        string buildPath = CssImportProcessor.ComputeOutputPathForSource(srcFile, workspace);

        buildPath.DirectoryName().Create();

        string processedContent = CssImportProcessor.ProcessForBuild(cssContent, srcFile, workspace, diagnostics);

        File.WriteAllText(buildPath, processedContent);
    }
}
