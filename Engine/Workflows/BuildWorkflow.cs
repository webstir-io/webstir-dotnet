using Engine.Extensions;
using Engine.Interfaces;
using Engine.Models;

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

        LogInfo($"Starting build (ReleaseMode: {releaseMode}, CleanBuild: {cleanBuild})...");

        // Auto-detect project mode from source directory BEFORE workspace setup
        var originalWorkingDir = _app.WorkingDir.FullName;
        _app.Initialize(workingDirectory.FullName);
        
        var projectMode = _app.DetectProjectMode();
        LogInfo($"Detected project mode: {projectMode}");
        
        // Now initialize workspace and restore original working dir context
        _app.Initialize(originalWorkingDir);
        InitializeWorkspace();

        // Copy source files to workspace if they exist
        if (workingDirectory.Exists)
        {
            CopyToWorkspace(workingDirectory);
        }
        
        // Execute workers to build in workspace
        await ExecuteWorkersAsync(async worker =>
        {
            await Task.Run(() => worker.Build(releaseMode));
        }, projectMode);

        LogInfo("Build completed successfully");
    }

    /// <summary>
    /// Determines if a clean build is needed based on arguments or build state
    /// </summary>
    private bool ShouldCleanBuild(string[] args)
    {
        var explicitClean = args.Contains(App.Options.Clean);
        if (explicitClean) return true;

        var buildWorkspaceDir = App.OutDir.CreateSubDirectory("build");
        var buildDir = buildWorkspaceDir.CreateSubDirectory(App.Folders.Build);
        
        if (!buildDir.Exists)
            return true;

        // Check for TypeScript build info files
        const string tsBuildInfoFile = ".tsbuildinfo";
        var clientBuildDir = buildDir.CreateSubDirectory(App.Folders.Client);
        var serverBuildDir = buildDir.CreateSubDirectory(App.Folders.Server);
        
        var clientTsConfig = clientBuildDir.GetFiles(tsBuildInfoFile).FirstOrDefault();
        var serverTsConfig = serverBuildDir.GetFiles(tsBuildInfoFile).FirstOrDefault();

        var clientSrcExists = _app.WorkingDir.CreateSubDirectory($"{App.Folders.Src}/{App.Folders.Client}").Exists;
        var serverSrcExists = _app.WorkingDir.CreateSubDirectory($"{App.Folders.Src}/{App.Folders.Server}").Exists;

        if (clientSrcExists && clientTsConfig == null)
            return true;
        if (serverSrcExists && serverTsConfig == null)
            return true;

        return false;
    }
}