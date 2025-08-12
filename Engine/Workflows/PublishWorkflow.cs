using Engine.Extensions;
using Engine.Modules;

namespace Engine.Workflows;

public class PublishWorkflow(AppContext context, IEnumerable<IAppModule> modules) : BaseWorkflow(context, modules)
{
    public override string WorkflowName => Commands.Publish;

    public override async Task ExecuteAsync(string[] args)
    {
        await ExecuteBuildAsync(releaseMode: true);
        await ExecuteWorkersAsync(async worker => await worker.Publish());
    }
}