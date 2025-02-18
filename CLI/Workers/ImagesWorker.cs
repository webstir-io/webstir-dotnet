namespace CLI.Workers;

public class ImagesWorker : IWebFileWorker
{
    public int BuildOrder { get; } = 4;

    public void Init()
    {
        return;
    }

    public void Build(bool releaseMode = false)
    {
        Directories.ImagesDirectory.CopyTo(Directories.BinImagesDirectory.FullName);
    }

    public void Publish()
    {
        Directories.BinImagesDirectory.CopyTo(Directories.DistImagesDirectory.FullName);
    }
}
