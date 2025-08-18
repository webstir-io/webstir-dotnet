using Engine.Services;
using Engine.Workers;
using Engine.Workers.Server;
using Engine.Workers.Shared;

namespace Engine.Workflows;

public class WatchWorkflow(
    AppContext context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker,
    WatchService watchService) 
    : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    public override string WorkflowName => Commands.Watch;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync();
        await watchService.Watch(Context, _ => ExecuteBuildAsync());
    }
}