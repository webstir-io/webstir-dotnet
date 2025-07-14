using Engine.Interfaces;
using Engine.Models;

namespace Engine.Modules;

public class SharedModule : IAppModule
{
    private readonly IEnumerable<ISharedWorker> _sharedWorkers;
    
    public SharedModule(IEnumerable<ISharedWorker> sharedWorkers)
    {
        _sharedWorkers = sharedWorkers;
    }

    public string Name => "Shared Module";
    
    public IEnumerable<IModuleWorker> Workers => _sharedWorkers.Cast<IModuleWorker>();

    public void Init(ProjectMode mode)
    {
        foreach (var worker in _sharedWorkers)
        {
            worker.Init(mode);
        }
    }

    public void Build(bool releaseMode)
    {
        foreach (var worker in _sharedWorkers.OrderBy(w => w.BuildOrder))
        {
            worker.Build(releaseMode);
        }
    }

    public void Publish()
    {
        foreach (var worker in _sharedWorkers.OrderBy(w => w.BuildOrder))
        {
            worker.Publish();
        }
    }
}
