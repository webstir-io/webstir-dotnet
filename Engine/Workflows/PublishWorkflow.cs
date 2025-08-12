using Engine.Extensions;

namespace Engine.Workflows;

public class PublishWorkflow(AppContext context) : BaseWorkflow(context)
{
    public override string WorkflowName => Commands.Publish;

    public override async Task ExecuteAsync(string[] args)
    {
        await ExecuteBuildAsync(releaseMode: true);
        await ExecuteWorkersAsync(async worker => await worker.Publish());
    }
}