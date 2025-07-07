using CLI.Models;

namespace CLI.Interfaces;

public interface IFileWorker
{
    int BuildOrder { get; }
    void Init(ProjectMode mode = ProjectMode.Fullstack);
    void Build(bool releaseMode = false);
    void Publish();
}