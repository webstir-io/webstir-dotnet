using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Media;

public class MediaHandler(AppWorkspace workspace, ILogger<MediaHandler> logger) : IFrontendHandler
{
    private static readonly string[] MediaExts = [
        FileExtensions.Mp3, FileExtensions.M4a, FileExtensions.Wav,
        FileExtensions.Ogg, FileExtensions.Mp4, FileExtensions.Webm,
        FileExtensions.Mov
    ];

    public int BuildOrder => 1;
    public int PublishOrder => 4;

    public Task<bool> BuildAsync(string? changedFilePath = null)
    {
        try
        {
            // If incremental and change is outside media, no-op
            if (!string.IsNullOrEmpty(changedFilePath))
            {
                string root = workspace.FrontendMediaPath;
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

            if (!workspace.FrontendMediaPath.Exists())
            {
                logger.LogWarning("[Media] Source directory does not exist: {Path}", workspace.FrontendMediaPath);
                return Task.FromResult(true);
            }

            Copy(workspace.FrontendMediaPath, workspace.FrontendBuildMediaPath, logger);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError("[Media] Error during build - {Message}", ex.Message);
            return Task.FromResult(false);
        }
    }

    public Task<bool> PublishAsync()
    {
        try
        {
            if (!workspace.FrontendBuildMediaPath.Exists())
            {
                logger.LogWarning("[Media] Build directory does not exist: {Path}", workspace.FrontendBuildMediaPath);
                return Task.FromResult(true);
            }

            Copy(workspace.FrontendBuildMediaPath, workspace.FrontendDistMediaPath, logger);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError("[Media] Error during publish - {Message}", ex.Message);
            return Task.FromResult(false);
        }
    }


    private static void Copy(string sourceRoot, string destRoot, ILogger logger)
    {
        string[] files = [.. sourceRoot.Files("*.*", SearchOption.AllDirectories)
            .Where(f => MediaExts.Contains(Path.GetExtension(f).ToLowerInvariant()))];

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
                logger.LogError("[Media] Error copying {File} - {Message}", srcFile, ex.Message);
                throw;
            }
        }
    }
}
