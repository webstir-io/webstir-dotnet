using Engine.Extensions;

namespace Engine.Handlers;

public class ImagesHandler(AppWorkspace workspace)
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico"];
    
    public async Task BuildAsync()
    {
        if (!workspace.ClientImagesPath.Exists())
        {
            await Task.CompletedTask;
            return;
        }

        string[] imageFiles = workspace.ClientImagesPath.Files("*.*", SearchOption.AllDirectories)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToArray();

        foreach (string srcFile in imageFiles)
        {
            string relativePath = Path.GetRelativePath(workspace.ClientImagesPath, srcFile);
            string buildPath = workspace.ClientBuildImagesPath.Combine(relativePath);
            Path.GetDirectoryName(buildPath)!.Create();
            File.Copy(srcFile, buildPath, true);
        }

        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        if (!workspace.ClientBuildImagesPath.Exists())
        {
            await Task.CompletedTask;
            return;
        }

        string[] imageFiles = workspace.ClientBuildImagesPath.Files("*.*", SearchOption.AllDirectories)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToArray();

        foreach (string srcFile in imageFiles)
        {
            string relativePath = Path.GetRelativePath(workspace.ClientBuildImagesPath, srcFile);
            string distPath = workspace.ClientDistImagesPath.Combine(relativePath);
            Path.GetDirectoryName(distPath)!.Create();
            File.Copy(srcFile, distPath, true);
        }

        await Task.CompletedTask;
    }

    public static async Task AddPageAsync(string pageName)
    {
        await Task.CompletedTask;
    }
}