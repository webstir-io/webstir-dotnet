using Engine.Interfaces;
using Engine.Models;

namespace Engine.Modules;

public class ServerModule : IAppModule
{
    private readonly IEnumerable<IServerWorker> _serverWorkers;
    
    public ServerModule(IEnumerable<IServerWorker> serverWorkers)
    {
        _serverWorkers = serverWorkers;
    }

    public string Name => "Server Module";
    
    public IEnumerable<IModuleWorker> Workers => _serverWorkers.Cast<IModuleWorker>();

    public void Init(ProjectMode mode)
    {
        foreach (var worker in _serverWorkers)
        {
            worker.Init(mode);
        }
    }

    public void Build(bool releaseMode)
    {
        foreach (var worker in _serverWorkers.OrderBy(w => w.BuildOrder))
        {
            worker.Build(releaseMode);
        }
    }

    public void Publish()
    {
        foreach (var worker in _serverWorkers.OrderBy(w => w.BuildOrder))
        {
            worker.Publish();
        }
    }
}
