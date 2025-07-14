using Engine.Extensions;
using Engine.Interfaces;
using Engine.Models;

namespace Engine;

public class App(IEnumerable<IAppModule> modules)
{
    public const string Name = "webstir";

    private string _workingFolder = string.Empty;

    public void Initialize(string workingFolder)
    {
        _workingFolder = workingFolder;
    }

    public IEnumerable<IAppModule> Modules => modules;
    
    /// <summary>
    /// Gets modules appropriate for the detected project type based on folder structure
    /// </summary>
    public IEnumerable<IAppModule> GetActiveModules(ProjectMode? mode = null)
    {
        var projectMode = mode ?? DetectProjectMode();
        
        return projectMode switch
        {
            ProjectMode.ClientOnly => modules.Where(m => m.Name.Contains("Client") || m.Name.Contains("Shared")),
            ProjectMode.ServerOnly => modules.Where(m => m.Name.Contains("Server") || m.Name.Contains("Shared")), 
            ProjectMode.Fullstack => modules, // All modules
            _ => modules
        };
    }
    
    /// <summary>
    /// Detects project type based on folder structure in working directory
    /// </summary>
    public ProjectMode DetectProjectMode()
    {
        // Check for existence without creating directories
        var clientPath = Path.Combine(WorkingDir.FullName, Folders.Src, Folders.Client);
        var serverPath = Path.Combine(WorkingDir.FullName, Folders.Src, Folders.Server);
        
        var hasClientDir = Directory.Exists(clientPath);
        var hasServerDir = Directory.Exists(serverPath);
        
        
        return (hasClientDir, hasServerDir) switch
        {
            (true, true) => ProjectMode.Fullstack,
            (true, false) => ProjectMode.ClientOnly,
            (false, true) => ProjectMode.ServerOnly,
            (false, false) => ProjectMode.Fullstack // Default for new projects
        };
    }

    // Global out directory for all workflows (relative to CLI project root)
    public static DirectoryInfo OutDir
    {
        get
        {
            // Get the directory where the CLI assembly is located
            var cliLocation = AppContext.BaseDirectory;
            var outPath = Path.Combine(cliLocation, Folders.Out);
            return Directory.CreateDirectory(outPath);
        }
    }
    
    // Project working directory (where source files are located)
    public DirectoryInfo WorkingDir => Directory.CreateDirectory(_workingFolder);
    public DirectoryInfo NodeModulesDir => WorkingDir.CreateSubDirectory(Folders.NodeModules);

    // Workflow workspace management - workflows use App directly by changing working directory
    public void InitializeWorkflowWorkspace(string workflowName)
    {
        var workflowDir = OutDir.CreateSubDirectory(workflowName);
        Initialize(workflowDir.FullName);
    }

    // Legacy directory accessors for current working directory structure
    public DirectoryInfo SrcDir => WorkingDir.CreateSubDirectory(Folders.Src);
    public DirectoryInfo BuildDir => WorkingDir.CreateSubDirectory(Folders.Build);
    public DirectoryInfo DistDir => WorkingDir.CreateSubDirectory(Folders.Dist);

    public DirectoryInfo ClientDir => SrcDir.CreateSubDirectory(Folders.Client);
    public DirectoryInfo ClientAppDir => ClientDir.CreateSubDirectory(Folders.App);
    public DirectoryInfo ClientPagesDir => ClientDir.CreateSubDirectory(Folders.Pages);
    public DirectoryInfo ClientIndexDir => ClientPagesDir.CreateSubDirectory(Folders.Index);
    public DirectoryInfo ClientImagesDir => ClientDir.CreateSubDirectory(Folders.Images);
    public DirectoryInfo ClientBuildDir => BuildDir.CreateSubDirectory(Folders.Client);
    public DirectoryInfo ClientBuildPagesDir => ClientBuildDir.CreateSubDirectory(Folders.Pages);
    public DirectoryInfo ClientBuildImagesDir => ClientBuildDir.CreateSubDirectory(Folders.Images);
    public DirectoryInfo ClientDistDir => DistDir.CreateSubDirectory(Folders.Client);
    public DirectoryInfo ClientDistImagesDir => ClientDistDir.CreateSubDirectory(Folders.Images);
    public DirectoryInfo ClientDistPagesDir => ClientDistDir.CreateSubDirectory(Folders.Pages);

    public DirectoryInfo ServerDir => SrcDir.CreateSubDirectory(Folders.Server);
    public DirectoryInfo ServerBuildDir => BuildDir.CreateSubDirectory(Folders.Server);
    public DirectoryInfo ServerDistDir => DistDir.CreateSubDirectory(Folders.Server);

    public DirectoryInfo SharedDir => SrcDir.CreateSubDirectory(Folders.Shared);

    public static class Commands
    {
        public const string Init = "init";
        public const string AddPage = "add-page";
        public const string Build = "build";
        public const string Watch = "watch";
        public const string Publish = "publish";
        public const string Help = "help";
        public const string Demo = "demo";
    }

    public static class Options
    {
        public const string ClientOnly = "--client-only";
        public const string ServerOnly = "--server-only";
        public const string Clean = "--clean";
        public const string Help = "--help";
        public const string HelpShort = "-h";
    }

    public static class Folders
    {
        public const string Out = "out";
        public const string Src = "src";
        public const string Build = "build";
        public const string Dist = "dist";
        public const string Client = "client";
        public const string Server = "server";
        public const string Shared = "shared";
        public const string App = "app";
        public const string Pages = "pages";
        public const string Styles = "styles";
        public const string Scripts = "scripts";
        public const string Images = "images";
        public const string Index = "index";
        public const string NodeModules = "node_modules";
        public const string Demo = "demo";
    }

    public static class Files
    {
        public const string PackageJson = "package.json";
    }
}