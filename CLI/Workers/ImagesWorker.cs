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
        Directories.ClientImagesDirectory.CopyTo(Directories.ClientBuildImagesDirectory.FullName);
    }

    public void Publish()
    {
        Directories.ClientBuildImagesDirectory.CopyTo(Directories.ClientDistImagesDirectory.FullName);
    }

    public void Add(DirectoryInfo pageDirectory)
    {
        return;
    }
}
