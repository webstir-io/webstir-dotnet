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

    public Task BuildAsync(string? changedFilePath = null)
    {
        // If incremental and change is outside fonts, no-op
        if (!string.IsNullOrEmpty(changedFilePath))
        {
            string root = workspace.ClientFontsPath;
            if (!root.Exists())
            {
                return Task.CompletedTask;
            }
            string relative = Path.GetRelativePath(root, changedFilePath!);
            bool isUnder = !relative.StartsWith("..", StringComparison.Ordinal);
            if (!isUnder)
            {
                return Task.CompletedTask;
            }
        }

        if (workspace.ClientFontsPath.Exists())
        {
            Copy(workspace.ClientFontsPath, workspace.ClientBuildFontsPath);
        }
        return Task.CompletedTask;
    }

    public Task PublishAsync()
    {
        if (workspace.ClientBuildFontsPath.Exists())
        {
            Copy(workspace.ClientBuildFontsPath, workspace.ClientDistFontsPath);
        }
        return Task.CompletedTask;
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
