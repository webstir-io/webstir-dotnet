namespace Framework.Packaging;

using System.Threading.Tasks;

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
