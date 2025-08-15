using Engine.Extensions;

namespace Engine.Handlers;

public class ImagesHandler(AppContext context)
{
    public async Task BuildAsync()
    {
        await context.ClientImagesPath.CopyToAsync(context.ClientBuildImagesPath);
    }

    public async Task PublishAsync()
    {
        await context.ClientBuildImagesPath.CopyToAsync(context.ClientDistImagesPath);
    }

    public async Task AddPageAsync(string name)
    {
        await Task.CompletedTask;
    }
}