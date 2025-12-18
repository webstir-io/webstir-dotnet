using System.Threading;
using System.Threading.Tasks;

namespace Framework.Packaging;

public interface IPackageWorkspace
{
    string WorkingPath
    {
        get;
    }

    string NodeModulesPath
    {
        get;
    }

    string WebstirPath
    {
        get;
    }

    PackageManagerDescriptor PackageManager
    {
        get;
    }

    Task InstallDependenciesAsync(CancellationToken cancellationToken = default);

    Task InstallPackagesAsync(string[] packageSpecs, CancellationToken cancellationToken = default);
}
