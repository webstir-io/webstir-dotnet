namespace CLI.Interfaces;

public interface IFileWorker
{
    int BuildOrder { get; }
    void Init();
    void Add(DirectoryInfo pageDirectory);
    void Build(bool releaseMode = false);
    void Publish();
}