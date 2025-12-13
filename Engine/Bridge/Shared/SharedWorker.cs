using System.Threading.Tasks;
using Engine.Helpers;
using Engine.Models;
using Engine.Interfaces;

namespace Engine.Bridge.Shared;

public class SharedWorker(AppWorkspace workspace) : IWorkflowWorker
{
    public int BuildOrder => 3; // Fast operation, can run with other fast operations

    public async Task InitAsync(WorkspaceProfile profile) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.SharedPath, workspace.SharedPath);

    public Task BuildAsync(string? changedFilePath = null) => Task.CompletedTask;

    public Task PublishAsync() => Task.CompletedTask;

}
