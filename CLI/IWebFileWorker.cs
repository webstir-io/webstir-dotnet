namespace CLI;

public interface IWebFileWorker
{
    int BuildOrder { get; }
    void Init();
    void Add(DirectoryInfo pageDirectory);
    void Build(bool releaseMode = false);
    void Publish();
}