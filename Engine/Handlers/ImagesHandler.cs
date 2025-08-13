using Engine.Extensions;
using Engine.Models;

namespace Engine.Handlers;

public class ImagesHandler(AppContext context) : IHandler
{
    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack)
    { 
        await Task.CompletedTask;
    }

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