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

    public string FrontendPath => SrcPath.CreateSubDirectory(Folders.Frontend);
    public string FrontendAppPath => FrontendPath.CreateSubDirectory(Folders.App);
    public string FrontendPagesPath => FrontendPath.CreateSubDirectory(Folders.Pages);
    public string FrontendImagesPath => FrontendPath.CreateSubDirectory(Folders.Images);
    public string FrontendFontsPath => FrontendPath.CreateSubDirectory(Folders.Fonts);
    public string FrontendMediaPath => FrontendPath.CreateSubDirectory(Folders.Media);
    public string FrontendBuildPath => BuildPath.CreateSubDirectory(Folders.Frontend);
    public string FrontendBuildAppPath => FrontendBuildPath.CreateSubDirectory(Folders.App);
    public string FrontendBuildPagesPath => FrontendBuildPath.CreateSubDirectory(Folders.Pages);
    public string FrontendBuildImagesPath => FrontendBuildPath.CreateSubDirectory(Folders.Images);
    public string FrontendBuildFontsPath => FrontendBuildPath.CreateSubDirectory(Folders.Fonts);
    public string FrontendBuildMediaPath => FrontendBuildPath.CreateSubDirectory(Folders.Media);
    public string FrontendDistPath => DistPath.CreateSubDirectory(Folders.Frontend);
    public string FrontendDistImagesPath => FrontendDistPath.CreateSubDirectory(Folders.Images);
    public string FrontendDistFontsPath => FrontendDistPath.CreateSubDirectory(Folders.Fonts);
    public string FrontendDistMediaPath => FrontendDistPath.CreateSubDirectory(Folders.Media);
    public string FrontendDistPagesPath => FrontendDistPath.CreateSubDirectory(Folders.Pages);
    public string FrontendDistAppPath => FrontendDistPath.CreateSubDirectory(Folders.App);

    public string BackendPath => SrcPath.CreateSubDirectory(Folders.Backend);
    public string BackendBuildPath => BuildPath.CreateSubDirectory(Folders.Backend);
    public string BackendDistPath => DistPath.CreateSubDirectory(Folders.Backend);

    public string SharedPath => SrcPath.CreateSubDirectory(Folders.Shared);

    public ProjectMode DetectProjectMode()
    {
        string clientPath = WorkingPath.Combine(Folders.Src, Folders.Frontend);
        string serverPath = WorkingPath.Combine(Folders.Src, Folders.Backend);

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
