using Engine.Servers;
using Engine.Models;
using Engine.Workers;
using Engine.Extensions;

namespace Engine.Workflows;

public abstract class BaseWorkflow(
    AppWorkspace context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker) : IWorkflow
{
    protected readonly AppWorkspace Context = context;
    public abstract string WorkflowName { get; }

    public virtual async Task ExecuteAsync(string[] args)
    {
        InitializeWorkspace(args);
        await ExecuteWorkflowAsync(args);
    }
    
    protected abstract Task ExecuteWorkflowAsync(string[] args);

    protected async Task ExecuteWorkersAsync(Func<IWorker, Task> workerAction, ProjectMode? mode = null)
    {
        ArgumentNullException.ThrowIfNull(workerAction);

        IEnumerable<IWorker> workers = GetFilteredWorkers(mode ?? Context.DetectProjectMode());
        
        IEnumerable<IGrouping<int, IWorker>> workerGroups = workers
            .GroupBy(w => w.BuildOrder)
            .OrderBy(g => g.Key);

        foreach (IGrouping<int, IWorker> group in workerGroups)
        {
            List<IWorker> workersInGroup = [.. group];
            foreach (IWorker worker in workersInGroup)
            {
                await workerAction(worker);
            }
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

    protected async Task ExecuteBuildAsync() => await ExecuteWorkersAsync(async worker => await worker.BuildAsync());

    protected async Task ExecuteBuildAsync(string? changedFilePath) =>
        await ExecuteWorkersAsync(async worker => await worker.BuildAsync(changedFilePath));

    protected virtual void InitializeWorkspace(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string[] filteredArgs = [.. args.Where(arg => arg != WorkflowName)];        
        string? projectName = GetProjectFromFlags(filteredArgs);
        
        if (!string.IsNullOrEmpty(projectName))
        {
            string projectPath = Context.WorkingPath.Combine(projectName);
            if (!Directory.Exists(projectPath))
                throw new DirectoryNotFoundException($"Project directory '{projectName}' not found in current directory");
            
            Context.Initialize(projectPath);
            return;
        }

        List<string> validProjects = [.. Context.WorkingPath.Folders()
            .Where(projectPath => projectPath.Combine(Folders.Src).Exists())];

        if (validProjects.Count == 0)
            throw new InvalidOperationException(
                "No valid webstir projects found in current directory. Run 'init <project-name>' to create a new project.");
        
        if (validProjects.Count == 1)
        {
            Context.Initialize(validProjects.Single());
            return;
        }
        
        IEnumerable<string?> projectNames = validProjects.Select(Path.GetFileName);
        throw new InvalidOperationException(
            $"Multiple projects found: {string.Join(", ", projectNames)}. " +
            $"Please specify which project to use: {WorkflowName} <project-name> or {WorkflowName} --project-name <project-name>");
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
