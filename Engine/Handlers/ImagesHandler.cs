using Engine.Extensions;

namespace Engine.Handlers;

public class ImagesHandler(AppWorkspace workspace)
{
    public async Task BuildAsync()
    {
        await workspace.ClientImagesPath.CopyToAsync(workspace.ClientBuildImagesPath);
    }

    public async Task PublishAsync()
    {
        await workspace.ClientBuildImagesPath.CopyToAsync(workspace.ClientDistImagesPath);
    }

    public async Task AddPageAsync(string name)
    {
        await Task.CompletedTask;
    }
}