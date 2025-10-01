using System.Threading.Tasks;
using Framework.Packaging;

namespace Engine.Bridge;

internal sealed class PackageWorkspaceAdapter(AppWorkspace workspace) : IPackageWorkspace
{
    private readonly AppWorkspace _workspace = workspace;

    public string WorkingPath => _workspace.WorkingPath;

    public string NodeModulesPath => _workspace.NodeModulesPath;

    public string WebstirPath => _workspace.WebstirPath;

    public Task RunNpmInstallAsync()
    {
        NpmHelper.RunNpmInstall(_workspace.WorkingPath);
        return Task.CompletedTask;
    }

    public Task InstallPackagesAsync(params string[] packageSpecs)
    {
        NpmHelper.RunNpmInstallPackages(_workspace.WorkingPath, packageSpecs);
        return Task.CompletedTask;
    }
}
