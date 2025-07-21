namespace Engine.Workflows;

/// <summary>
/// Builds the project in an isolated workspace
/// </summary>
public class BuildWorkflow : BaseWorkflow
{
    public BuildWorkflow(App app) 
        : base(app)
    {
    }

    public override string WorkflowName => "build";

    public override async Task ExecuteAsync(string[] args)
    {
        // Parse parameters from args
        var workingDirectory = _app.WorkingDir;
        var releaseMode = false; // Build workflow always uses debug mode
        var cleanBuild = ShouldCleanBuild(args);

        // Call the base build logic
        await ExecuteBuildAsync(workingDirectory, releaseMode, cleanBuild);
    }
}