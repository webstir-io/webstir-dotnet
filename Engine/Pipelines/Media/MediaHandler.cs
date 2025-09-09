using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core.Interfaces;

namespace Engine.Pipelines.Media;

public class MediaHandler(AppWorkspace workspace) : IFrontendHandler
{
    private static readonly string[] MediaExts = [
        FileExtensions.Mp3, FileExtensions.M4a, FileExtensions.Wav,
        FileExtensions.Ogg, FileExtensions.Mp4, FileExtensions.Webm,
        FileExtensions.Mov
    ];

    public int BuildOrder => 1;
    public int PublishOrder => 4;

    public Task BuildAsync(string? changedFilePath = null)
    {
        // If incremental and change is outside media, no-op
        if (!string.IsNullOrEmpty(changedFilePath))
        {
            string root = workspace.ClientMediaPath;
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

        if (workspace.ClientMediaPath.Exists())
        {
            Copy(workspace.ClientMediaPath, workspace.ClientBuildMediaPath);
        }
        return Task.CompletedTask;
    }

    public Task PublishAsync()
    {
        if (workspace.ClientBuildMediaPath.Exists())
        {
            Copy(workspace.ClientBuildMediaPath, workspace.ClientDistMediaPath);
        }
        return Task.CompletedTask;
    }


    private static void Copy(string sourceRoot, string destRoot)
    {
        string[] files = [.. sourceRoot.Files("*.*", SearchOption.AllDirectories)
            .Where(f => MediaExts.Contains(Path.GetExtension(f).ToLowerInvariant()))];

        foreach (string srcFile in files)
        {
            string relativePath = Path.GetRelativePath(sourceRoot, srcFile);
            string destPath = destRoot.Combine(relativePath);
            Path.GetDirectoryName(destPath)!.Create();
            File.Copy(srcFile, destPath, true);
        }
    }
}
