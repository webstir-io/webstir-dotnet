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
    string ToolsPath
    {
        get;
    }
    Task RunNpmInstallAsync();
    Task InstallPackagesAsync(params string[] packageSpecs);
}
