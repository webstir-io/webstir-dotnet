using CLI.Helpers;
using CLI.Interfaces;
using CLI.Models;
using CLI.Builders.Demo;
using static CLI.Constants.Commands;
using System.Diagnostics;

namespace CLI;

public class Runner(IEnumerable<IFileWorker> _fileWorkers, Watcher _watcher)
{    
    public async Task Run(string[] args)
    {
        var command = args.Length != 0 
            ? args.First() 
            : string.Empty;
        
        if (IsHelpRequested(command, args))
            return;

        await ExecuteCommand(command, args);
    }

    private static bool IsHelpRequested(string command, string[] args)
    {
        if (command == HelpCommand || command == HelpOption || command == HelpShortOption)
        {
            if (args.Length > 1 && command == HelpCommand)
                Helper.ShowCommandHelp(args[1]);
            else
                Helper.ShowGeneralHelp();
            return true;
        }
        
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

            case DemoCommand:
                Demo(args[1..]);
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
        return ProjectMode.Fullstack;
    }

    private void AddPage(string[] args)
    {
        var pageName = args.FirstOrDefault();   
        if (string.IsNullOrEmpty(pageName))
        {
            Console.WriteLine($"Usage: {Webstir} {AddPageCommand} <page-name>");
            return;
        }
        
        var pagePath = Directories.ClientPagesDirectory.Join(pageName);   
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
        if (!Directory.Exists(Directories.BuildDirectory.FullName))
            return true;

        const string tsBuildInfoFile = ".tsbuildinfo";
        var clientTsConfig = Directories.ClientBuildDirectory.Join(tsBuildInfoFile);
        var serverTsConfig = Directories.ServerBuildDirectory.Join(tsBuildInfoFile);

        if (Directories.ClientDirectory.Exists && !File.Exists(clientTsConfig))
            return true;
        if (Directories.ServerDirectory.Exists && !File.Exists(serverTsConfig))
            return true;

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

    private void Demo(string[] args)
    {
        var targetDirectory = args.FirstOrDefault() ?? Settings.DemoFolder;
        
        if (!Path.IsPathRooted(targetDirectory))
        {
            targetDirectory = Path.Combine(Directory.GetCurrentDirectory(), targetDirectory);
        }
        
        // If the directory exists, delete it to ensure a clean demo
        if (Directory.Exists(targetDirectory))
        {
            Console.WriteLine($"Removing existing demo directory at {targetDirectory}...");
            Directory.Delete(targetDirectory, recursive: true);
        }
        
        var demoBuilder = new DemoBuilder(_fileWorkers);
        demoBuilder.CreateTemplate(targetDirectory);
        
        // Start webstir in the demo directory
        Console.WriteLine();
        Console.WriteLine($"Starting {Webstir} in demo directory...");
        Console.WriteLine();
        
        var processInfo = new ProcessStartInfo
        {
            FileName = Webstir,
            WorkingDirectory = targetDirectory,
            UseShellExecute = false
        };
        
        using var process = Process.Start(processInfo);
        process?.WaitForExit();
    }
}