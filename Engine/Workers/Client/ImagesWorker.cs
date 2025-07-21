using Engine.Extensions;
using Engine.Models;

namespace Engine.Workers.Client;

public class ImagesWorker(App app) : IClientWorker
{
    public int BuildOrder => 3; // Fast operations can run together after TS compilation

    public void Init(ProjectMode mode = ProjectMode.Fullstack) { }

    public void Build(bool releaseMode = false)
    {
        app.ClientImagesDir.CopyTo(app.ClientBuildImagesDir.FullName);
    }

    public void Publish()
    {
        app.ClientBuildImagesDir.CopyTo(app.ClientDistImagesDir.FullName);
    }

    public void AddPage(DirectoryInfo pageDirectory) { }

    public void AddPage(string name) { }
}
