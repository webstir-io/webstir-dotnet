using Engine.Workers.Shared;

namespace Engine.Modules;

public class SharedModule(IEnumerable<ISharedWorker> workers) : BaseModule(workers)
{
    public override string Name => "Shared Module";
}
