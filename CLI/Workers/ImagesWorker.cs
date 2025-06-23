using CLI.Interfaces;

namespace CLI.Workers;

public class ImagesWorker : IFileWorker
{
    public int BuildOrder { get; } = 4;

    public void Init()
    {
        return;
    }

    public void Build(bool releaseMode = false)
    {
        Directories.ImagesDirectory.CopyTo(Directories.BuildImagesDirectory.FullName);
    }

    public void Publish()
    {
        Directories.BuildImagesDirectory.CopyTo(Directories.DistImagesDirectory.FullName);
    }

    public void Add(DirectoryInfo pageDirectory)
    {
        return;
    }
}
