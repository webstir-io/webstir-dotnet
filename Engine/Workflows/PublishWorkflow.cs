using System.Threading.Tasks;
using System.Collections.Generic;
using Engine.Workflows.Interfaces;

namespace Engine.Workflows;

public class PublishWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers) : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.Publish;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync();
        await ExecuteWorkersAsync(async worker => await worker.PublishAsync());
    }
}
