using Engine.Modules;

namespace Engine.Workflows;

public class BuildWorkflow(AppContext context, IEnumerable<IAppModule> modules) : BaseWorkflow(context, modules)
{
    public override string WorkflowName => Commands.Build;

    public override async Task ExecuteAsync(string[] args)
    {
        // TODO: Implement clean build logic
        var cleanBuild = args.Contains(BuildOptions.Clean);
        await ExecuteBuildAsync();
    }
}