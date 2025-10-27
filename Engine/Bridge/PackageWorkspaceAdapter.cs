using System.Threading;
using System.Threading.Tasks;
using Framework.Packaging;

namespace Engine.Bridge;

internal sealed class PackageWorkspaceAdapter(AppWorkspace workspace) : IPackageWorkspace
{
    private readonly AppWorkspace _workspace = workspace;

    public string WorkingPath => _workspace.WorkingPath;

    public string NodeModulesPath => _workspace.NodeModulesPath;

    public string WebstirPath => _workspace.WebstirPath;

    public PackageManagerDescriptor PackageManager => CreateRunner().Descriptor;

    public Task InstallDependenciesAsync(CancellationToken cancellationToken = default) =>
        CreateRunner().InstallDependenciesAsync(cancellationToken);

    public Task InstallPackagesAsync(string[] packageSpecs, CancellationToken cancellationToken = default) =>
        CreateRunner().InstallPackagesAsync(packageSpecs, cancellationToken);

    private PackageManagerRunner CreateRunner() => PackageManagerRunner.Create(_workspace.WorkingPath);
}
