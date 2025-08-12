using Engine.Servers;
using Engine.Models;
using Engine.Workers;
using Engine.Modules;

namespace Engine.Workflows;

public abstract class BaseWorkflow(AppContext context, IEnumerable<IAppModule> modules) : IWorkflow
{
    protected readonly AppContext Context = context;
    protected readonly IEnumerable<IAppModule> Modules = modules;
    public abstract string WorkflowName { get; }

    public abstract Task ExecuteAsync(string[] args);

    protected async Task ExecuteWorkersAsync(Func<IModuleWorker, Task> workerAction, ProjectMode? mode = null)
    {
        var activeModules = Context.FilterModules(Modules, mode);
        var workers = activeModules.SelectMany(m => m.Workers);

        var workerGroups = workers
            .GroupBy(w => w.BuildOrder)
            .OrderBy(g => g.Key);

        foreach (var group in workerGroups)
        {
            var workersInGroup = group.ToList();
            // Temporarily running sequentially for debugging
            foreach (var worker in workersInGroup)
            {
                await workerAction(worker);
            }
            // await Task.WhenAll(workersInGroup.Select(workerAction));            
        }
    }

    // TODO: Implement clean build logic
    protected async Task ExecuteBuildAsync(bool releaseMode = false)
    {
        await ExecuteWorkersAsync(async worker => await worker.Build(releaseMode));
    }
}