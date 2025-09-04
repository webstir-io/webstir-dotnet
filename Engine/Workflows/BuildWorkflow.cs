using System.Threading.Tasks;
using Engine.Workers;

namespace Engine.Workflows;

public class BuildWorkflow(
    AppWorkspace context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker)
    : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    public override string WorkflowName => Commands.Build;

    protected override async Task ExecuteWorkflowAsync(string[] args) => await ExecuteBuildAsync();
}
