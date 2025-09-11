using System.IO;
using System.Text;
using System.Threading.Tasks;
using Engine.Pipelines.Core.Interfaces;

namespace Engine.Pipelines.Seo;

public class RobotsTxtHandler(AppWorkspace workspace) : IFrontendHandler
{
    public int BuildOrder => 3;
    public int PublishOrder => 3;

    public async Task BuildAsync(string? changedFilePath = null)
    {
        string path = Path.Combine(workspace.FrontendBuildPath, Files.RobotsTxt);
        await WriteAllowAllAsync(path);
    }

    public async Task PublishAsync()
    {
        string path = Path.Combine(workspace.FrontendDistPath, Files.RobotsTxt);
        await WriteAllowAllAsync(path);
    }

    private static async Task WriteAllowAllAsync(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string content = "User-agent: *\nAllow: /\n";
        await File.WriteAllTextAsync(path, content, Encoding.ASCII);
    }
}

