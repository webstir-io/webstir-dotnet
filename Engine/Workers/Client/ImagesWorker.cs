using Engine.Extensions;
using Engine.Models;

namespace Engine.Workers.Client;

public class ImagesWorker(AppContext context) : IClientWorker
{
    public int BuildOrder => 3; // Fast operations can run together after TS compilation

    public async Task Init(ProjectMode mode = ProjectMode.Fullstack)
    { 
        await Task.CompletedTask;
    }

    public async Task Build(bool releaseMode = false)
    {
        context.ClientImagesPath.CopyTo(context.ClientBuildImagesPath);
        await Task.CompletedTask;
    }

    public async Task Publish()
    {
        context.ClientBuildImagesPath.CopyTo(context.ClientDistImagesPath);
        await Task.CompletedTask;
    }

    public async Task AddPage(string name)
    {
        await Task.CompletedTask;
    }
}
