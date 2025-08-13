using Engine.Servers;
using Engine.Models;
using Engine.Workers;
using Engine.Workers.Server;
using Engine.Workers.Shared;

namespace Engine.Workflows;

public abstract class BaseWorkflow(
    AppContext context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker) : IWorkflow
{
    protected readonly AppContext Context = context;
    protected readonly ClientWorker ClientWorker = clientWorker;
    protected readonly ServerWorker ServerWorker = serverWorker;
    protected readonly SharedWorker SharedWorker = sharedWorker;
    public abstract string WorkflowName { get; }

    public abstract Task ExecuteAsync(string[] args);

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
            {
                await workerAction(worker);
            }
        }
    }

    private IEnumerable<IWorker> GetFilteredWorkers(ProjectMode mode)
    {
        return mode switch
        {
            ProjectMode.ClientOnly => [ClientWorker],
            ProjectMode.ServerOnly => [ServerWorker, SharedWorker],
            _ => [ClientWorker, ServerWorker, SharedWorker]
        };
    }

    protected async Task ExecuteBuildAsync(bool releaseMode = false)
    {
        await ExecuteWorkersAsync(async worker => await worker.BuildAsync(releaseMode));
    }
}