using System.Threading.Tasks;
using Framework.Packaging;

namespace Engine.Bridge;

internal sealed class PackageWorkspaceAdapter(AppWorkspace workspace) : IPackageWorkspace
{
    private readonly AppWorkspace _workspace = workspace;

    public string WorkingPath => _workspace.WorkingPath;

    public string NodeModulesPath => _workspace.NodeModulesPath;

    public string WebstirPath => _workspace.WebstirPath;

    public async Task RunNpmInstallAsync() =>
        await NpmHelper.RunNpmInstallAsync(_workspace.WorkingPath).ConfigureAwait(false);

    public async Task InstallPackagesAsync(params string[] packageSpecs) =>
        await NpmHelper.RunNpmInstallPackagesAsync(_workspace.WorkingPath, packageSpecs).ConfigureAwait(false);
}
