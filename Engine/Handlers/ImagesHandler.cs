using Engine.Extensions;

namespace Engine.Handlers;

public class ImagesHandler(AppContext context)
{
    public async Task BuildAsync(bool releaseMode = false)
    {
        context.ClientImagesPath.CopyTo(context.ClientBuildImagesPath);
        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        context.ClientBuildImagesPath.CopyTo(context.ClientDistImagesPath);
        await Task.CompletedTask;
    }

    public async Task AddPageAsync(string name)
    {
        await Task.CompletedTask;
    }
}