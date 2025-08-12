using Engine.Extensions;
using Engine.Models;
using Engine.Modules;

namespace Engine;

public class AppContext(IEnumerable<IAppModule> modules)
{
    private string _workingFolder = string.Empty;

    public void Initialize(string workingFolder)
    {
        _workingFolder = workingFolder;
    }

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
    public string ClientBuildPagesPath => ClientBuildPath.CreateSubDirectory(Folders.Pages);
    public string ClientBuildImagesPath => ClientBuildPath.CreateSubDirectory(Folders.Images);
    public string ClientDistPath => DistPath.CreateSubDirectory(Folders.Client);
    public string ClientDistImagesPath => ClientDistPath.CreateSubDirectory(Folders.Images);
    public string ClientDistPagesPath => ClientDistPath.CreateSubDirectory(Folders.Pages);

    public string ServerPath => SrcPath.CreateSubDirectory(Folders.Server);
    public string ServerBuildPath => BuildPath.CreateSubDirectory(Folders.Server);
    public string ServerDistPath => DistPath.CreateSubDirectory(Folders.Server);

    public string SharedPath => SrcPath.CreateSubDirectory(Folders.Shared);

    public IEnumerable<IAppModule> ActiveModules(ProjectMode? mode = null)
    {
        var projectMode = mode ?? DetectProjectMode();

        return projectMode switch
        {
            ProjectMode.ClientOnly => modules.Where(m => m.Name.Contains(Folders.Client)),
            ProjectMode.ServerOnly => modules.Where(m => m.Name.Contains(Folders.Server)),
            _ => modules
        };
    }

    public ProjectMode DetectProjectMode()
    {
        var clientPath = WorkingPath.Combine(Folders.Src, Folders.Client);
        var serverPath = WorkingPath.Combine(Folders.Src, Folders.Server);

        var hasClientDir = Directory.Exists(clientPath);
        var hasServerDir = Directory.Exists(serverPath);

        return (hasClientDir, hasServerDir) switch
        {
            (true, true) => ProjectMode.Fullstack,
            (true, false) => ProjectMode.ClientOnly,
            (false, true) => ProjectMode.ServerOnly,
            (false, false) => ProjectMode.Fullstack
        };
    }
}