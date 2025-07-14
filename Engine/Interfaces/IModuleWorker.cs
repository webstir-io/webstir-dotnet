using Engine.Models;

namespace Engine.Interfaces;

public interface IModuleWorker
{
    int BuildOrder { get; }
    void Init(ProjectMode mode);
    void Build(bool releaseMode);
    void Publish();
    void AddPage(DirectoryInfo pageDirectory);
}
