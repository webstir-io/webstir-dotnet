using Engine.Helpers;
using Engine.Models;

namespace Engine.Workers;

public class SharedWorker(AppWorkspace workspace) : IWorker
{
    public int BuildOrder => 3; // Fast operation, can run with other fast operations

    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack)
    {
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.SharedResourcesPath, workspace.SharedPath);
    }

    public async Task BuildAsync()
    { 
        await Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        await Task.CompletedTask;
    }

    public async Task AddPageAsync(string pageName)
    {
        await Task.CompletedTask;
    }
}