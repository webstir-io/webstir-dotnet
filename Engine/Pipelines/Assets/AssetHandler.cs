using Engine.Extensions;

namespace Engine.Pipelines.Assets;

public class AssetHandler(AppWorkspace workspace)
{
    private static readonly string[] ImageExts = [".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico"];

    public Task BuildAsync()
    {
        if (workspace.ClientImagesPath.Exists())
        {
            CopyImages(workspace.ClientImagesPath, workspace.ClientBuildImagesPath);
        }
        return Task.CompletedTask;
    }

    public Task PublishAsync()
    {
        if (workspace.ClientBuildImagesPath.Exists())
        {
            CopyImages(workspace.ClientBuildImagesPath, workspace.ClientDistImagesPath);
        }
        return Task.CompletedTask;
    }

    private static void CopyImages(string sourceRoot, string destRoot)
    {
        string[] imageFiles = [.. sourceRoot.Files("*.*", SearchOption.AllDirectories)
            .Where(f => ImageExts.Contains(Path.GetExtension(f).ToLowerInvariant()))];

        foreach (string srcFile in imageFiles)
        {
            string relativePath = Path.GetRelativePath(sourceRoot, srcFile);
            string destPath = destRoot.Combine(relativePath);
            Path.GetDirectoryName(destPath)!.Create();
            File.Copy(srcFile, destPath, true);
        }
    }

    public static Task AddPageAsync(string pageName) => Task.CompletedTask;
}
