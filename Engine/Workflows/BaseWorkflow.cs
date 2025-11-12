using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Models;
using Engine.Interfaces;

namespace Engine.Workflows;

public abstract class BaseWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers) : IWorkflow
{
    protected readonly AppWorkspace Context = context;
    protected IEnumerable<IWorkflowWorker> Workers { get; } = workers;
    protected IFrontendWorker Frontend => Workers.OfType<IFrontendWorker>().Single();
    public abstract string WorkflowName
    {
        get;
    }

    public virtual async Task ExecuteAsync(string[] args)
    {
        InitializeWorkspace(args);
        await ExecuteWorkflowAsync(args);
    }

    protected abstract Task ExecuteWorkflowAsync(string[] args);

    protected async Task ExecuteWorkersAsync(Func<IWorkflowWorker, Task> workerAction, ProjectMode? mode = null)
    {
        ArgumentNullException.ThrowIfNull(workerAction);

        IEnumerable<IWorkflowWorker> workers = GetFilteredWorkers(mode ?? Context.DetectProjectMode());

        IEnumerable<IGrouping<int, IWorkflowWorker>> workerGroups = workers
            .GroupBy(w => w.BuildOrder)
            .OrderBy(g => g.Key);

        foreach (IGrouping<int, IWorkflowWorker> group in workerGroups)
        {
            List<IWorkflowWorker> workersInGroup = [.. group];
            foreach (IWorkflowWorker worker in workersInGroup)
            {
                await workerAction(worker);
            }
        }
    }

    private IEnumerable<IWorkflowWorker> GetFilteredWorkers(ProjectMode mode)
    {
        return mode switch
        {
            ProjectMode.ClientOnly => Workers.Where(w => w is IFrontendWorker),
            ProjectMode.ServerOnly => Workers.Where(w => w is not IFrontendWorker),
            _ => Workers
        };
    }

    protected async Task ExecuteBuildAsync() => await ExecuteBuildAsync(Context.DetectProjectMode());

    protected async Task ExecuteBuildAsync(ProjectMode mode) =>
        await ExecuteWorkersAsync(async worker => await worker.BuildAsync(), mode);

    protected async Task ExecuteBuildAsync(string? changedFilePath) =>
        await ExecuteBuildAsync(changedFilePath, Context.DetectProjectMode());

    protected async Task ExecuteBuildAsync(string? changedFilePath, ProjectMode mode) =>
        await ExecuteWorkersAsync(async worker => await worker.BuildAsync(changedFilePath), mode);

    protected virtual void InitializeWorkspace(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string[] filteredArgs = [.. args.Where(arg => arg != WorkflowName)];
        string? projectName = GetProjectFromFlags(filteredArgs);

        if (!string.IsNullOrEmpty(projectName))
        {
            string projectPath = Context.WorkingPath.Combine(projectName);
            if (!projectPath.Exists())
                throw new WorkflowUsageException($"Project directory '{projectName}' not found in current directory.");

            Context.Initialize(projectPath);
            return;
        }

        List<string> validProjects = [.. Context.WorkingPath.Folders()
            .Where(projectPath => projectPath.Combine(Folders.Src).Exists())];

        if (validProjects.Count == 0)
            throw new WorkflowUsageException(
                "No webstir project found here. Run 'webstir init <name>' first or pass the target path (e.g., 'webstir build ../my-app').");

        if (validProjects.Count == 1)
        {
            Context.Initialize(validProjects.Single());
            return;
        }

        IEnumerable<string?> projectNames = validProjects.Select(Path.GetFileName);
        throw new WorkflowUsageException(
            $"Multiple projects found: {string.Join(", ", projectNames)}. " +
            $"Specify which project to use: {WorkflowName} <project-name> or {WorkflowName} --project-name <project-name>.");
    }

    protected static string? GetProjectFromFlags(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (int index = 0; index < args.Length; index++)
        {
            if ((args[index] == ProjectOptions.ProjectName || args[index] == ProjectOptions.ProjectNameShort) && index + 1 < args.Length)
                return args[index + 1];
        }

        return null;
    }
}
