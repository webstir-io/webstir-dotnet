namespace Engine.Workflows;

public class BuildWorkflow(AppContext context) : BaseWorkflow(context)
{
    public override string WorkflowName => Commands.Build;

    public override async Task ExecuteAsync(string[] args)
    {
        // TODO: Implement clean build logic
        var cleanBuild = args.Contains(BuildOptions.Clean);
        await ExecuteBuildAsync();
    }
}