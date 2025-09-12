using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core.Interfaces;

namespace Engine.Pipelines.Fonts;

public class FontsHandler(AppWorkspace workspace) : IFrontendHandler
{
    private static readonly string[] FontExts = [
        FileExtensions.Woff2, FileExtensions.Woff, FileExtensions.Ttf,
        FileExtensions.Otf, FileExtensions.Eot, FileExtensions.Svg
    ];

    public int BuildOrder => 1;
    public int PublishOrder => 3;

    public async Task BuildAsync(string? changedFilePath = null)
    {
        // If incremental and change is outside fonts, no-op
        if (!string.IsNullOrEmpty(changedFilePath))
        {
            string root = workspace.FrontendFontsPath;
            if (!root.Exists())
            {
                return;
            }
            string relative = Path.GetRelativePath(root, changedFilePath!);
            bool isUnder = !relative.StartsWith("..", StringComparison.Ordinal);
            if (!isUnder)
            {
                return;
            }
        }

        if (workspace.FrontendFontsPath.Exists())
        {
            await FontOptimizer.OptimizeAsync(workspace.FrontendFontsPath, workspace.FrontendBuildFontsPath);
        }
        return;
    }

    public async Task PublishAsync()
    {
        if (workspace.FrontendBuildFontsPath.Exists())
        {
            Copy(workspace.FrontendBuildFontsPath, workspace.FrontendDistFontsPath);
        }
        await Task.CompletedTask;
        return;
    }


    private static void Copy(string sourceRoot, string destRoot)
    {
        string[] files = [.. sourceRoot.Files("*.*", SearchOption.AllDirectories)
            .Where(f => FontExts.Contains(Path.GetExtension(f).ToLowerInvariant()))];

        foreach (string srcFile in files)
        {
            string relativePath = Path.GetRelativePath(sourceRoot, srcFile);
            string destPath = destRoot.Combine(relativePath);
            Path.GetDirectoryName(destPath)!.Create();
            File.Copy(srcFile, destPath, true);
        }
    }
}
