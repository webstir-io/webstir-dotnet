using Engine.Workers;
using Engine.Workers.Server;
using Engine.Workers.Shared;

namespace Engine.Workflows;

public class BuildWorkflow(
    AppContext context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker) : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    public override string WorkflowName => Commands.Build;

    public override async Task ExecuteAsync(string[] args)
    {
        // TODO: Implement clean build logic
        var cleanBuild = args.Contains(BuildOptions.Clean);
        await ExecuteBuildAsync();
    }
}