namespace CLI;

public interface IWebFileWorker
{
    int BuildOrder { get; }
    void Init();
    void Build(bool releaseMode = false);
    void Publish();
}