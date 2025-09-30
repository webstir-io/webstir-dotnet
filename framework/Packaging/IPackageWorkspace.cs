namespace Framework.Packaging;

using System.Threading.Tasks;

public interface IPackageWorkspace
{
    string WorkingPath { get; }
    string NodeModulesPath { get; }
    string ToolsPath { get; }
    Task RunNpmInstallAsync();
}
