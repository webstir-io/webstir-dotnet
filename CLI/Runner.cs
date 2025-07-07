using CLI.Helpers;
using CLI.Interfaces;
using CLI.Models;
using static CLI.Constants.Commands;

namespace CLI;

public class Runner(IEnumerable<IFileWorker> _fileWorkers, Watcher _watcher)
{    
    public async Task Run(string[] args)
    {
        var command = args.Length != 0 
            ? args.First() 
            : string.Empty;
        
        // Handle help requests
        if (IsHelpRequested(command, args))
            return;

        // Execute the command
        await ExecuteCommand(command, args);
    }

    private static bool IsHelpRequested(string command, string[] args)
    {
        // Check for help flags
        if (command == HelpCommand || command == HelpOption || command == HelpShortOption)
        {
            if (args.Length > 1 && command == HelpCommand)
                Helper.ShowCommandHelp(args[1]);
            else
                Helper.ShowGeneralHelp();
            return true;
        }
        
        // Check for command-specific help
        if (args.Length > 1 && (args[1] == HelpOption || args[1] == HelpShortOption))
        {
            Helper.ShowCommandHelp(command);
            return true;
        }

        return false;
    }

    private async Task ExecuteCommand(string command, string[] args)
    {
        switch (command)
        {
            case InitCommand:
                Init(args[1..]);
                break;
            
            case AddPageCommand:
                AddPage(args[1..]);
                break;

            case BuildCommand:
                Build(cleanBuild: args.Contains(CleanOption));
                break;

            case "":
            case WatchCommand:
                Build();
                await Watch();
                break;

            case PublishCommand:
                Publish();
                break;

            default:
                ShowUnknownCommandError(command);
                break;
        }
    }

    private static void ShowUnknownCommandError(string command)
    {
        Console.WriteLine($"Unknown command '{command}'");
        Console.WriteLine();
        Console.WriteLine($"Run '{Webstir} {HelpCommand}' to see available commands.");
    }

    private void Init(string[] args)
    {
        var mode = ParseProjectMode(args);
        
        foreach (var worker in _fileWorkers)
            worker.Init(mode);
        
        // Copy package.json if it doesn't exist
        var packageJsonPath = Path.Combine(Directory.GetCurrentDirectory(), Settings.PackageJsonFile);
        if (!File.Exists(packageJsonPath))
        {
            AssemblyHelpers.WriteResourceToFile(Settings.PackageJsonFile, packageJsonPath);
        }
    }
    
    private static ProjectMode ParseProjectMode(string[] args)
    {
        if (args.Contains(ClientOnlyOption)) return ProjectMode.ClientOnly;
        if (args.Contains(ServerOnlyOption)) return ProjectMode.ServerOnly;
        return ProjectMode.Fullstack; // Default
    }

    private void AddPage(string[] args)
    {
        var pageName = args.FirstOrDefault();   
        if (string.IsNullOrEmpty(pageName))
        {
            Console.WriteLine($"Usage: {Webstir} {AddPageCommand} <page-name>");
            return;
        }
        
        var pagePath = Directories.PagesDirectory.Join(pageName);   
        if (Directory.Exists(pagePath))
        {
            Console.WriteLine($"Page '{pageName}' already exists at {pagePath}");
            return;
        }
        
        var pageDirectory = Directory.CreateDirectory(pagePath);
        Console.WriteLine($"Creating page '{pageName}'...");
        foreach (var worker in _fileWorkers.OfType<IPageWorker>())
            worker.AddPage(pageDirectory);     

        Console.WriteLine($"✓ Created page at {pagePath}");
    }

    private void Build(bool releaseMode = false, bool cleanBuild = false)
    {
        Console.Write("Building...");

        if (cleanBuild || ShouldCleanBuild())
        {
            Console.Write(" (clean)");
            if (Directory.Exists(Directories.BuildDirectory.FullName))
            {
                foreach (var directory in Directories.BuildDirectory.GetDirectories())
                    directory.Delete(true);
            }
        }

        foreach (var worker in _fileWorkers.OrderBy(p => p.BuildOrder))
            worker.Build(releaseMode);

        Console.WriteLine(" Done");
    }

    private static bool ShouldCleanBuild()
    {
        // Clean build if no build directory exists
        if (!Directory.Exists(Directories.BuildDirectory.FullName))
            return true;

        // Clean build if tsconfig files don't match Resources
        const string tsBuildInfoFile = ".tsbuildinfo";
        var clientTsConfig = Directories.ClientBuildDirectory.Join(tsBuildInfoFile);
        var serverTsConfig = Directories.ServerBuildDirectory.Join(tsBuildInfoFile);

        // If in fullstack mode and either buildinfo is missing, clean build
        if (Directories.ClientDirectory.Exists && !File.Exists(clientTsConfig))
            return true;
        if (Directories.ServerDirectory.Exists && !File.Exists(serverTsConfig))
            return true;

        // TODO: Add more intelligent checks like config file changes
        return false;
    }

    private void Publish()
    {
        Build(true);

        Console.Write("Publishing...");

        Directories.DistDirectory.Delete(true);

        foreach (var worker in _fileWorkers)
            worker.Publish();
            
        Console.WriteLine("Done");
    }

    private async Task Watch()
    {
        await _watcher.Watch(cleanBuild => Build(cleanBuild: cleanBuild));
    }
}