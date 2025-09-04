using System.IO;
using Engine.Extensions;
using Engine.Models;

namespace Engine;

public class AppWorkspace
{
    private string _workingFolder = string.Empty;

    public void Initialize(string workingFolder) => _workingFolder = workingFolder;

    public string WorkingPath => Directory.CreateDirectory(_workingFolder).FullName;
    public string NodeModulesPath => WorkingPath.CreateSubDirectory(Folders.NodeModules);

    public string SrcPath => WorkingPath.CreateSubDirectory(Folders.Src);
    public string BuildPath => WorkingPath.CreateSubDirectory(Folders.Build);
    public string DistPath => WorkingPath.CreateSubDirectory(Folders.Dist);

    public string ClientPath => SrcPath.CreateSubDirectory(Folders.Client);
    public string ClientAppPath => ClientPath.CreateSubDirectory(Folders.App);
    public string ClientPagesPath => ClientPath.CreateSubDirectory(Folders.Pages);
    public string ClientImagesPath => ClientPath.CreateSubDirectory(Folders.Images);
    public string ClientBuildPath => BuildPath.CreateSubDirectory(Folders.Client);
    public string ClientBuildAppPath => ClientBuildPath.CreateSubDirectory(Folders.App);
    public string ClientBuildPagesPath => ClientBuildPath.CreateSubDirectory(Folders.Pages);
    public string ClientBuildImagesPath => ClientBuildPath.CreateSubDirectory(Folders.Images);
    public string ClientDistPath => DistPath.CreateSubDirectory(Folders.Client);
    public string ClientDistImagesPath => ClientDistPath.CreateSubDirectory(Folders.Images);
    public string ClientDistPagesPath => ClientDistPath.CreateSubDirectory(Folders.Pages);
    public string ClientDistAppPath => ClientDistPath.CreateSubDirectory(Folders.App);

    public string ServerPath => SrcPath.CreateSubDirectory(Folders.Server);
    public string ServerBuildPath => BuildPath.CreateSubDirectory(Folders.Server);
    public string ServerDistPath => DistPath.CreateSubDirectory(Folders.Server);

    public string SharedPath => SrcPath.CreateSubDirectory(Folders.Shared);

    public ProjectMode DetectProjectMode()
    {
        string clientPath = WorkingPath.Combine(Folders.Src, Folders.Client);
        string serverPath = WorkingPath.Combine(Folders.Src, Folders.Server);

        bool hasClientDir = Directory.Exists(clientPath);
        bool hasServerDir = Directory.Exists(serverPath);

        return (hasClientDir, hasServerDir) switch
        {
            (true, true) => ProjectMode.Fullstack,
            (true, false) => ProjectMode.ClientOnly,
            (false, true) => ProjectMode.ServerOnly,
            (false, false) => ProjectMode.Fullstack
        };
    }
}
