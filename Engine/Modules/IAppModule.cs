using Engine.Models;
using Engine.Workers;

namespace Engine.Modules;

public interface IAppModule
{
    string Name { get; }
    IEnumerable<IModuleWorker> Workers { get; }
    void Init(ProjectMode mode);
    void Build(bool releaseMode);
    void Publish();
}