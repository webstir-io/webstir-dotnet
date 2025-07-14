using Engine.Extensions;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workflows;

/// <summary>
/// Base class for all workflows providing common functionality
/// </summary>
public abstract class BaseWorkflow : IWorkflow
{
    protected readonly App _app;

    protected BaseWorkflow(App app)
    {
        _app = app;
    }
    
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


}