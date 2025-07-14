using Engine.Extensions;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workflows;

/// <summary>
/// Creates a production build in an isolated workspace
/// </summary>
public class PublishWorkflow : BaseWorkflow<PublishParameters>
{
    private readonly BuildWorkflow _buildWorkflow;

    public PublishWorkflow(App app, BuildWorkflow buildWorkflow) 
        : base(app)
    {
        _buildWorkflow = buildWorkflow;
    }

    public override string WorkflowName => "publish";

    public override async Task ExecuteAsync(PublishParameters parameters)
    {
        LogInfo("Starting publish (production build)...");

        // First run build workflow with release mode
        var buildParameters = new BuildParameters
        {
            WorkingDirectory = parameters.WorkingDirectory,
            ReleaseMode = true, // Publish always uses release mode
            CleanBuild = parameters.CleanBuild
        };

        LogInfo("Running build phase...");
        await _buildWorkflow.ExecuteAsync(buildParameters);

        // Initialize App to point to publish workspace
        InitializeWorkspace();

        // Copy build workspace to publish workspace  
        var buildWorkspaceDir = App.OutDir.CreateSubDirectory("build");
        if (buildWorkspaceDir.Exists)
        {
            buildWorkspaceDir.CopyTo(_app.WorkingDir.FullName);
        }

        // Auto-detect project mode and execute only relevant workers
        var projectMode = _app.DetectProjectMode();
        
        // Execute workers in parallel to publish from build/ to dist/
        await ExecuteWorkersAsync(async worker =>
        {
            await Task.Run(() => worker.Publish());
        }, projectMode);

        LogInfo("Publish completed successfully");
        LogInfo($"Production files available in: {_app.DistDir.FullName}");
    }
}