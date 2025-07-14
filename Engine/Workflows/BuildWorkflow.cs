using Engine.Extensions;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workflows;

/// <summary>
/// Builds the project in an isolated workspace
/// </summary>
public class BuildWorkflow : BaseWorkflow<BuildParameters>
{
    public BuildWorkflow(App app) 
        : base(app)
    {
    }

    public override string WorkflowName => "build";

    public override async Task ExecuteAsync(BuildParameters parameters)
    {
        LogInfo($"Starting build (ReleaseMode: {parameters.ReleaseMode}, CleanBuild: {parameters.CleanBuild})...");

        // Auto-detect project mode from source directory BEFORE workspace setup
        var originalWorkingDir = _app.WorkingDir.FullName;
        _app.Initialize(parameters.WorkingDirectory.FullName);
        
        
        var projectMode = _app.DetectProjectMode();
        LogInfo($"Detected project mode: {projectMode}");
        
        // Now initialize workspace and restore original working dir context
        _app.Initialize(originalWorkingDir);
        InitializeWorkspace();

        // Copy source files to workspace if they exist
        if (parameters.WorkingDirectory.Exists)
        {
            CopyToWorkspace(parameters.WorkingDirectory);
        }
        
        // Execute workers to build in workspace
        await ExecuteWorkersAsync(async worker =>
        {
            await Task.Run(() => worker.Build(parameters.ReleaseMode));
        }, projectMode);

        LogInfo("Build completed successfully");
    }

    /// <summary>
    /// Checks if a clean build is needed based on workspace state
    /// </summary>
    public bool ShouldCleanBuild(DirectoryInfo workingDirectory)
    {
        var workspaceDir = GetWorkspaceDir();
        var buildDir = workspaceDir.CreateSubDirectory(App.Folders.Build);
        
        if (!buildDir.Exists)
            return true;

        // Check for TypeScript build info files
        const string tsBuildInfoFile = ".tsbuildinfo";
        var clientBuildDir = buildDir.CreateSubDirectory(App.Folders.Client);
        var serverBuildDir = buildDir.CreateSubDirectory(App.Folders.Server);
        
        var clientTsConfig = clientBuildDir.GetFiles(tsBuildInfoFile).FirstOrDefault();
        var serverTsConfig = serverBuildDir.GetFiles(tsBuildInfoFile).FirstOrDefault();

        var clientSrcExists = workingDirectory.CreateSubDirectory($"{App.Folders.Src}/{App.Folders.Client}").Exists;
        var serverSrcExists = workingDirectory.CreateSubDirectory($"{App.Folders.Src}/{App.Folders.Server}").Exists;

        if (clientSrcExists && clientTsConfig == null)
            return true;
        if (serverSrcExists && serverTsConfig == null)
            return true;

        return false;
    }
}