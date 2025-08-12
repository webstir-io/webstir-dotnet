using Engine.Models;

namespace Engine.Workers;

public interface IModuleWorker
{
    int BuildOrder { get; }
    Task Init(ProjectMode mode);
    Task Build(bool releaseMode);
    Task Publish();
}
