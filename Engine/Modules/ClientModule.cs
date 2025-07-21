using Engine.Workers.Client;

namespace Engine.Modules;

public class ClientModule(IEnumerable<IClientWorker> workers) : BaseModule(workers)
{
    public override string Name => "Client Module";
}