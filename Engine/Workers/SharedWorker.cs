using System.Threading.Tasks;
using Engine.Helpers;
using Engine.Models;

namespace Engine.Workers;

public class SharedWorker(AppWorkspace workspace) : IWorker
{
    public int BuildOrder => 3; // Fast operation, can run with other fast operations

    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Templates.SharedPath, workspace.SharedPath);

    public Task BuildAsync(string? changedFilePath = null) => Task.CompletedTask;

    public Task PublishAsync() => Task.CompletedTask;

    public Task AddPageAsync(string pageName) => Task.CompletedTask;
}
