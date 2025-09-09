using System.Threading.Tasks;
using Engine.Workers;

namespace Engine.Workflows;

public class PublishWorkflow(
    AppWorkspace context,
    FrontendWorker clientWorker,
    BackendWorker serverWorker,
    SharedWorker sharedWorker) : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    public override string WorkflowName => Commands.Publish;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync();
        await ExecuteWorkersAsync(async worker => await worker.PublishAsync());
    }
}
