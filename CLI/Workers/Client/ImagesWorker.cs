using CLI.Interfaces;
using CLI.Models;

namespace CLI.Workers;

public class ImagesWorker : IFileWorker
{
    public int BuildOrder { get; } = 4;

    public void Init(ProjectMode mode = ProjectMode.Fullstack)
    {
        // Images worker doesn't need initialization
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
}
