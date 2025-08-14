using Engine.Servers;
using Engine.Models;
using Engine.Workers;
using Engine.Workers.Server;
using Engine.Workers.Shared;
using Engine.Extensions;

namespace Engine.Workflows;

public abstract class BaseWorkflow(
    AppContext context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker) : IWorkflow
{
    protected readonly AppContext Context = context;
    public abstract string WorkflowName { get; }

    public virtual async Task ExecuteAsync(string[] args)
    {
        InitializeWorkspace(args);
        await ExecuteWorkflowAsync(args);
    }
    
    protected abstract Task ExecuteWorkflowAsync(string[] args);

    protected async Task ExecuteWorkersAsync(Func<IWorker, Task> workerAction, ProjectMode? mode = null)
    {
        var workers = GetFilteredWorkers(mode ?? Context.DetectProjectMode());
        
        var workerGroups = workers
            .GroupBy(w => w.BuildOrder)
            .OrderBy(g => g.Key);

        foreach (var group in workerGroups)
        {
            var workersInGroup = group.ToList();
            foreach (var worker in workersInGroup)
                await workerAction(worker);
        }
    }

    private IEnumerable<IWorker> GetFilteredWorkers(ProjectMode mode)
    {
        return mode switch
        {
            ProjectMode.ClientOnly => [clientWorker],
            ProjectMode.ServerOnly => [serverWorker, sharedWorker],
            _ => [clientWorker, serverWorker, sharedWorker]
        };
    }

    protected async Task ExecuteBuildAsync(bool releaseMode = false)
    {
        await ExecuteWorkersAsync(async worker => await worker.BuildAsync(releaseMode));
    }

    protected virtual void InitializeWorkspace(string[] args)
    {
        var filteredArgs = args.Where(arg => arg != WorkflowName).ToArray();
        var projectName = filteredArgs.FirstOrDefault();
        
        if (!string.IsNullOrEmpty(projectName))
        {
            var projectPath = Context.WorkingPath.Combine(projectName);
            if (!Directory.Exists(projectPath))
                throw new DirectoryNotFoundException($"Project directory '{projectName}' not found in current directory");
            
            Context.Initialize(projectPath);
            return;
        }

        var validProjects = Context.WorkingPath.Folders()
            .Where(projectPath => projectPath.Combine(Folders.Src).Exists())
            .ToList();

        if (validProjects.Count == 0)
            throw new InvalidOperationException(
                "No valid webstir projects found in current directory. Run 'init <project-name>' to create a new project.");
        
        if (validProjects.Count == 1)
        {
            Context.Initialize(validProjects.Single());
            return;
        }
        
        var projectNames = validProjects.Select(Path.GetFileName);
        throw new InvalidOperationException(
            $"Multiple projects found: {string.Join(", ", projectNames)}. " +
            $"Please specify which project to use: {WorkflowName} <project-name>");
    }

}