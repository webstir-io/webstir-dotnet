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
    Task RunNpmInstallAsync();
    Task InstallPackagesAsync(params string[] packageSpecs);
}
