using System.Threading.Tasks;
using System.Collections.Generic;
using Engine.Interfaces;

namespace Engine.Workflows;

public class BuildWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers)
    : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.Build;

    protected override async Task ExecuteWorkflowAsync(string[] args) => await ExecuteBuildAsync();
}
