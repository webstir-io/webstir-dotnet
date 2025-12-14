using System.IO;
using System.Threading.Tasks;
using Engine.Helpers;
using Engine.Models;
using Engine.Interfaces;
using Engine.Workflows;

namespace Engine.Bridge.Shared;

public class SharedWorker(AppWorkspace workspace) : IWorkflowWorker
{
    public int BuildOrder => 3; // Fast operation, can run with other fast operations

    public Task InitAsync(WorkspaceProfile profile)
    {
        if (profile.Mode is not (WorkspaceMode.Spa or WorkspaceMode.Full))
        {
            return Task.CompletedTask;
        }

        if (Directory.Exists(workspace.SharedPath) && Directory.GetFileSystemEntries(workspace.SharedPath).Length > 0)
        {
            return Task.CompletedTask;
        }

        throw new WorkflowUsageException(
            $"Shared scaffold is missing at '{workspace.SharedPath}'. " +
            $"Run '{App.Name} {Commands.Repair} {RepairOptions.DryRun}' to see what will be restored, then '{App.Name} {Commands.Repair}'.");
    }

    public Task BuildAsync(string? changedFilePath = null) => Task.CompletedTask;

    public Task PublishAsync() => Task.CompletedTask;

}
