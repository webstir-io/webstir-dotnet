using Engine.Models;

namespace Engine.Interfaces;

public interface IFileWorker
{
    int BuildOrder { get; }
    void Init(ProjectMode mode = ProjectMode.Fullstack);
    void Build(bool releaseMode = false);
    void Publish();
}