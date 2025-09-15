using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Images;

public class ImagesHandler(AppWorkspace workspace, ILogger<ImagesHandler> logger) : IFrontendHandler
{
    private static readonly string[] ImageExts = [
        FileExtensions.Png, FileExtensions.Jpg, FileExtensions.Jpeg,
        FileExtensions.Gif, FileExtensions.Svg, FileExtensions.Webp,
        FileExtensions.Ico
    ];

    public int BuildOrder => 1;
    public int PublishOrder => 2;

    public Task<bool> BuildAsync(string? changedFilePath = null)
    {
        try
        {
            // If incremental and change is outside images, no-op
            if (!string.IsNullOrEmpty(changedFilePath))
            {
                string root = workspace.FrontendImagesPath;
                if (!root.Exists())
                {
                    return Task.FromResult(true);
                }
                string relative = Path.GetRelativePath(root, changedFilePath!);
                bool isUnder = !relative.StartsWith("..", StringComparison.Ordinal);
                if (!isUnder)
                {
                    return Task.FromResult(true);
                }
            }

            if (!workspace.FrontendImagesPath.Exists())
            {
                logger.LogWarning("[Images] Source directory does not exist: {Path}", workspace.FrontendImagesPath);
                return Task.FromResult(true);
            }

            Copy(workspace.FrontendImagesPath, workspace.FrontendBuildImagesPath, logger);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError("[Images] Error during build - {Message}", ex.Message);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> PublishAsync()
    {
        try
        {
            if (!workspace.FrontendBuildImagesPath.Exists())
            {
                logger.LogWarning("[Images] Build directory does not exist: {Path}", workspace.FrontendBuildImagesPath);
                return true;
            }

            await ImageOptimizer.OptimizeAsync(workspace.FrontendBuildImagesPath, workspace.FrontendDistImagesPath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("[Images] Error during publish - {Message}", ex.Message);
            return false;
        }
    }


    private static void Copy(string sourceRoot, string destRoot, ILogger logger)
    {
        string[] files = [.. sourceRoot.Files("*.*", SearchOption.AllDirectories)
            .Where(f => ImageExts.Contains(Path.GetExtension(f).ToLowerInvariant()))];

        foreach (string srcFile in files)
        {
            try
            {
                string relativePath = Path.GetRelativePath(sourceRoot, srcFile);
                string destPath = destRoot.Combine(relativePath);
                Path.GetDirectoryName(destPath)!.Create();
                File.Copy(srcFile, destPath, true);
            }
            catch (Exception ex)
            {
                logger.LogError("[Images] Error copying {File} - {Message}", srcFile, ex.Message);
                throw;
            }
        }
    }
}
