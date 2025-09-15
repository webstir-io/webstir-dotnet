using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Fonts;

public class FontsHandler(AppWorkspace workspace, ILogger<FontsHandler> logger) : IFrontendHandler
{
    private static readonly string[] FontExts = [
        FileExtensions.Woff2, FileExtensions.Woff, FileExtensions.Ttf,
        FileExtensions.Otf, FileExtensions.Eot, FileExtensions.Svg
    ];

    public int BuildOrder => 1;
    public int PublishOrder => 3;

    public async Task<bool> BuildAsync(string? changedFilePath = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(changedFilePath))
            {
                string root = workspace.FrontendFontsPath;
                if (!root.Exists())
                {
                    return true;
                }
                string relative = Path.GetRelativePath(root, changedFilePath!);
                bool isUnder = !relative.StartsWith("..", StringComparison.Ordinal);
                if (!isUnder)
                {
                    return true;
                }
            }

            if (!workspace.FrontendFontsPath.Exists())
            {
                logger.LogWarning("[Fonts] Source directory does not exist: {Path}", workspace.FrontendFontsPath);
                return true;
            }

            await FontOptimizer.OptimizeAsync(workspace.FrontendFontsPath, workspace.FrontendBuildFontsPath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("[Fonts] Error during build - {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> PublishAsync()
    {
        try
        {
            if (!workspace.FrontendBuildFontsPath.Exists())
            {
                logger.LogWarning("[Fonts] Build directory does not exist: {Path}", workspace.FrontendBuildFontsPath);
                return true;
            }

            Copy(workspace.FrontendBuildFontsPath, workspace.FrontendDistFontsPath, logger);
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("[Fonts] Error during publish - {Message}", ex.Message);
            return false;
        }
    }


    private static void Copy(string sourceRoot, string destRoot, ILogger logger)
    {
        string[] files = [.. sourceRoot.Files("*.*", SearchOption.AllDirectories)
            .Where(f => FontExts.Contains(Path.GetExtension(f).ToLowerInvariant()))];

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
                logger.LogError("[Fonts] Error copying {File} - {Message}", srcFile, ex.Message);
                throw;
            }
        }
    }
}
