using Engine.Models;

namespace Engine.Handlers;

public interface IHandler
{
    Task InitAsync(ProjectMode mode);
    Task BuildAsync(bool releaseMode);
    Task PublishAsync();
    Task AddPageAsync(string pageName);
}