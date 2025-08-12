using Engine.Servers;
using Engine.Models;
using Engine.Workers;

namespace Engine.Workflows;

public abstract class BaseWorkflow(AppContext context) : IWorkflow
{
    protected readonly AppContext Context = context;
    public abstract string WorkflowName { get; }

    public abstract Task ExecuteAsync(string[] args);

    protected async Task ExecuteWorkersAsync(Func<IModuleWorker, Task> workerAction, ProjectMode? mode = null)
    {
        var activeWorkers = Context
            .ActiveModules(mode)
            .SelectMany(m => m.Workers);

        var workerGroups = activeWorkers
            .GroupBy(w => w.BuildOrder)
            .OrderBy(g => g.Key);

        foreach (var group in workerGroups)
        {
            var workersInGroup = group.ToList();                    
            await Task.WhenAll(workersInGroup.Select(workerAction));            
        }
    }

    // TODO: Implement clean build logic
    protected async Task ExecuteBuildAsync(bool releaseMode = false)
    {
        await ExecuteWorkersAsync(async worker => await worker.Build(releaseMode));
    }
}