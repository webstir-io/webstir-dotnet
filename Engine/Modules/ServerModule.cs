using Engine.Workers.Server;

namespace Engine.Modules;

public class ServerModule(IEnumerable<IServerWorker> workers) : BaseModule(workers)
{
    public override string Name => "Server Module";
}
