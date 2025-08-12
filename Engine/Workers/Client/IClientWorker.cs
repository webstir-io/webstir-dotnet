namespace Engine.Workers.Client;

public interface IClientWorker : IModuleWorker
{
    Task AddPage(string name);
}
