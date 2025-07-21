using Engine.Models;

namespace Engine.Workers;

public interface IModuleWorker
{
    int BuildOrder { get; }
    void Init(ProjectMode mode);
    void Build(bool releaseMode);
    void Publish();
    void AddPage(DirectoryInfo pageDirectory);
}
