using Engine.Interfaces;
using Engine.Models;

namespace Engine.Modules;

public class ClientModule : IAppModule
{
    private readonly IEnumerable<IClientWorker> _clientWorkers;
    
    public ClientModule(IEnumerable<IClientWorker> clientWorkers)
    {
        _clientWorkers = clientWorkers;
    }

    public string Name => "Client Module";
    
    public IEnumerable<IModuleWorker> Workers => _clientWorkers.Cast<IModuleWorker>();

    public void Init(ProjectMode mode)
    {
        foreach (var worker in _clientWorkers)
        {
            worker.Init(mode);
        }
    }

    public void Build(bool releaseMode)
    {
        foreach (var worker in _clientWorkers.OrderBy(w => w.BuildOrder))
        {
            worker.Build(releaseMode);
        }
    }

    public void Publish()
    {
        foreach (var worker in _clientWorkers.OrderBy(w => w.BuildOrder))
        {
            worker.Publish();
        }
    }
}