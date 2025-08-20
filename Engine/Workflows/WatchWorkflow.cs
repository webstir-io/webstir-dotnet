using Engine.Services;
using Engine.Workers;

namespace Engine.Workflows;

public class WatchWorkflow(
    AppWorkspace context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker,
    DevService devService) 
    : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    public override string WorkflowName => Commands.Watch;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync();
        await devService.StartAsync(Context, (filePath, _) => ExecuteBuildAsync(filePath));
    }
}