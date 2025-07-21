namespace Engine.Workers.Client;

public interface IClientWorker : IModuleWorker
{
    void AddPage(string name);
}
