using Engine.Models;
using Engine.Workers;

namespace Engine.Modules;

public abstract class BaseModule(IEnumerable<IModuleWorker> workers) : IAppModule
{
    public abstract string Name { get; }

    public IEnumerable<IModuleWorker> Workers => workers;

    public virtual void Init(ProjectMode mode)
    {
        foreach (var worker in workers)
        {
            worker.Init(mode);
        }
    }

    public virtual void Build(bool releaseMode)
    {
        foreach (var worker in workers.OrderBy(w => w.BuildOrder))
        {
            worker.Build(releaseMode);
        }
    }

    public virtual void Publish()
    {
        foreach (var worker in workers.OrderBy(w => w.BuildOrder))
        {
            worker.Publish();
        }
    }
}