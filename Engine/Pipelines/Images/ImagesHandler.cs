using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core.Interfaces;

namespace Engine.Pipelines.Images;

public class ImagesHandler(AppWorkspace workspace) : IFrontendHandler
{
    private static readonly string[] ImageExts = [
        FileExtensions.Png, FileExtensions.Jpg, FileExtensions.Jpeg,
        FileExtensions.Gif, FileExtensions.Svg, FileExtensions.Webp,
        FileExtensions.Ico
    ];

    public int BuildOrder => 1;
    public int PublishOrder => 2;

    public Task BuildAsync(string? changedFilePath = null)
    {
        // If incremental and change is outside images, no-op
        if (!string.IsNullOrEmpty(changedFilePath))
        {
            string root = workspace.ClientImagesPath;
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

        if (workspace.ClientImagesPath.Exists())
        {
            Copy(workspace.ClientImagesPath, workspace.ClientBuildImagesPath);
        }
        return Task.CompletedTask;
    }

    public Task PublishAsync()
    {
        if (workspace.ClientBuildImagesPath.Exists())
        {
            Copy(workspace.ClientBuildImagesPath, workspace.ClientDistImagesPath);
        }
        return Task.CompletedTask;
    }


    private static void Copy(string sourceRoot, string destRoot)
    {
        string[] files = [.. sourceRoot.Files("*.*", SearchOption.AllDirectories)
            .Where(f => ImageExts.Contains(Path.GetExtension(f).ToLowerInvariant()))];

        foreach (string srcFile in files)
        {
            string relativePath = Path.GetRelativePath(sourceRoot, srcFile);
            string destPath = destRoot.Combine(relativePath);
            Path.GetDirectoryName(destPath)!.Create();
            File.Copy(srcFile, destPath, true);
        }
    }
}
