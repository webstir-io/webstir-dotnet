using Engine.Models;

namespace Engine.Workers;

public interface IWorker
{
    int BuildOrder
    {
        get;
    }
    Task InitAsync(ProjectMode mode);
    Task BuildAsync(string? changedFilePath = null);
    Task PublishAsync();
    Task AddPageAsync(string pageName);
}
