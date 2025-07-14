using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workflows;

/// <summary>
/// Initializes a new webstir project in an isolated workspace
/// </summary>
public class InitWorkflow : BaseWorkflow
{
    public InitWorkflow(App app) 
        : base(app)
    {
    }

    public override string WorkflowName => "init";

    public override async Task ExecuteAsync(string[] args)
    {
        LogInfo("Starting project initialization...");

        // Parse parameters from args
        var mode = ParseProjectMode(args);
        var workingDirectory = _app.WorkingDir;

        // Initialize App to point to init workspace
        InitializeWorkspace();

        // Execute workers to create initial project structure in workspace
        await ExecuteWorkersAsync(async worker =>
        {
            LogInfo($"Initializing {worker.GetType().Name}...");
            await Task.Run(() => worker.Init(mode));
        }, mode);

        // Create package.json in workspace
        await CreatePackageJson();

        // Copy workspace to target directory
        await CopyWorkspaceToTarget(workingDirectory);

        LogInfo($"Project initialized successfully at {workingDirectory.FullName}");
    }

    private async Task CreatePackageJson()
    {
        var packageJsonPath = _app.WorkingDir.CombinePath(App.Files.PackageJson);
        
        if (!File.Exists(packageJsonPath))
        {
            LogInfo("Creating package.json...");
            await Task.Run(() => 
                AssemblyHelpers.WriteResourceToFile(App.Files.PackageJson, packageJsonPath)
            );
        }
    }

    private async Task CopyWorkspaceToTarget(DirectoryInfo targetDir)
    {
        LogInfo($"Copying project files to {targetDir.FullName}...");
        
        await Task.Run(() =>
        {
            // Ensure target directory exists
            if (!targetDir.Exists)
                targetDir.Create();

            // Copy all files from workspace to target directory
            _app.WorkingDir.CopyTo(targetDir.FullName);
        });
    }

    /// <summary>
    /// Parses project mode from command line arguments
    /// </summary>
    private ProjectMode ParseProjectMode(string[] args)
    {
        if (args.Contains(App.Options.ClientOnly)) return ProjectMode.ClientOnly;
        if (args.Contains(App.Options.ServerOnly)) return ProjectMode.ServerOnly;
        return ProjectMode.Fullstack;
    }
}