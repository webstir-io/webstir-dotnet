using Engine.Extensions;
using Engine.Servers;
using Engine.Models;
using Engine.Workers;

namespace Engine.Workflows;

/// <summary>
/// Base class for all workflows providing common functionality
/// </summary>
public abstract class BaseWorkflow(App app) : IWorkflow
{
    protected readonly App _app = app;

    /// <summary>
    /// Gets workers from active modules based on project type
    /// </summary>
    protected IEnumerable<IModuleWorker> GetActiveWorkers(ProjectMode? mode = null)
    {
        return _app.GetActiveModules(mode).SelectMany(m => m.Workers);
    }

    public abstract string WorkflowName { get; }

    public abstract Task ExecuteAsync(string[] args);

    /// <summary>
    /// Initializes App to point to this workflow's workspace
    /// </summary>
    protected void InitializeWorkspace()
    {
        _app.InitializeWorkflowWorkspace(WorkflowName);
    }

    /// <summary>
    /// Gets the current workflow workspace directory
    /// </summary>
    protected DirectoryInfo GetWorkspaceDir()
    {
        return App.OutDir.CreateSubDirectory(WorkflowName);
    }

    /// <summary>
    /// Executes workers in parallel where safe, respecting build order for dependencies
    /// </summary>
    protected async Task ExecuteWorkersAsync(Func<IModuleWorker, Task> workerAction, ProjectMode? mode = null)
    {
        var activeWorkers = GetActiveWorkers(mode);
        
        // Group workers by build order to respect dependencies
        var workerGroups = activeWorkers
            .GroupBy(w => w.BuildOrder)
            .OrderBy(g => g.Key);

        foreach (var group in workerGroups)
        {
            var workersInGroup = group.ToList();
            
            if (workersInGroup.Count == 1)
            {
                LogInfo($"Executing {workersInGroup[0].GetType().Name} (BuildOrder {group.Key})");
            }
            else
            {
                var workerNames = string.Join(", ", workersInGroup.Select(w => w.GetType().Name));
                LogInfo($"Executing {workersInGroup.Count} workers in parallel (BuildOrder {group.Key}): {workerNames}");
            }
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Execute workers within the same build order in parallel
            await Task.WhenAll(workersInGroup.Select(workerAction));
            
            stopwatch.Stop();
            LogInfo($"Completed BuildOrder {group.Key} in {stopwatch.ElapsedMilliseconds}ms");
        }
    }

    /// <summary>
    /// Copies files from source directory to App's current working directory
    /// </summary>
    protected void CopyToWorkspace(DirectoryInfo sourceDir)
    {
        if (sourceDir.Exists)
        {
            sourceDir.CopyTo(_app.WorkingDir.FullName);
        }
    }

    /// <summary>
    /// Logs workflow execution
    /// </summary>
    protected void LogInfo(string message)
    {
        Console.WriteLine($"[{WorkflowName}] {message}");
    }

    protected void LogError(string message)
    {
        Console.WriteLine($"[{WorkflowName}] ERROR: {message}");
    }

    /// <summary>
    /// Executes the build workflow with common logic shared between Build and Publish
    /// </summary>
    protected async Task ExecuteBuildAsync(DirectoryInfo workingDirectory, bool releaseMode, bool cleanBuild)
    {
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
    protected bool ShouldCleanBuild(string[] args)
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