using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Engine.Pipelines.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Seo;

public class RobotsTxtHandler(AppWorkspace workspace, ILogger<RobotsTxtHandler> logger) : IFrontendHandler
{
    public int BuildOrder => 3;
    public int PublishOrder => 3;

    public async Task<bool> BuildAsync(string? changedFilePath = null)
    {
        try
        {
            string path = Path.Combine(workspace.FrontendBuildPath, Files.RobotsTxt);
            await WriteAllowAllAsync(path);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("[SEO] Error creating robots.txt - {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> PublishAsync()
    {
        try
        {
            string path = Path.Combine(workspace.FrontendDistPath, Files.RobotsTxt);
            await WriteAllowAllAsync(path);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("[SEO] Error publishing robots.txt - {Message}", ex.Message);
            return false;
        }
    }

    private static async Task WriteAllowAllAsync(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string content = "User-agent: *\nAllow: /\n";
        await File.WriteAllTextAsync(path, content, Encoding.ASCII);
    }
}

