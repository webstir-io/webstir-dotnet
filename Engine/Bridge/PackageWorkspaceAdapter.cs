using System.Threading.Tasks;

using Engine;
using Framework.Packaging;

namespace Engine.Bridge;

internal sealed class PackageWorkspaceAdapter : IPackageWorkspace
{
    private readonly AppWorkspace _workspace;

    public PackageWorkspaceAdapter(AppWorkspace workspace)
    {
        _workspace = workspace;
    }

    public string WorkingPath => _workspace.WorkingPath;

    public string NodeModulesPath => _workspace.NodeModulesPath;

    public string ToolsPath => _workspace.ToolsPath;

    public Task RunNpmInstallAsync()
    {
        NpmHelper.RunNpmInstall(_workspace.WorkingPath);
        return Task.CompletedTask;
    }
}
