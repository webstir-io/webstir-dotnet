using Engine;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Engine.Builders.Demo;
using System.Diagnostics;
using static CLI.Constants;

namespace CLI;

public class Runner(IEnumerable<IFileWorker> _fileWorkers, Watcher _watcher)
{    
    public async Task Run(string[] args)
    {
        var command = args.Length != 0 
            ? args.First() 
            : string.Empty;
        
        // For help commands, don't extract path parameter
        if (IsHelpRequested(command, args))
            return;
        
        // Extract path parameter if provided for other commands
        var remainingArgs = ExtractAndSetWorkingDirectory(args);
        
        await ExecuteCommand(command, remainingArgs);
    }

    private static string[] ExtractAndSetWorkingDirectory(string[] args)
    {
        // Check if the last argument is a path (not starting with -- or -)
        if (args.Length > 0)
        {
            var lastArg = args[args.Length - 1];
            
            // If it's not an option and it's not a known command, treat it as a path
            if (!lastArg.StartsWith("-") && args.Length > 1)
            {
                Settings.WorkingDirectory = lastArg;
                return args.Take(args.Length - 1).ToArray();
            }
        }
        
        return args;
    }

    private static bool IsHelpRequested(string command, string[] args)
    {
        if (command == HelpCommand || command == HelpOption || command == HelpShortOption)
        {
            if (args.Length > 1 && command == HelpCommand)
                Help.ShowCommandHelp(args[1]);
            else
                Help.ShowGeneralHelp();
            return true;
        }
        
        if (args.Length > 1 && (args[1] == HelpOption || args[1] == HelpShortOption))
        {
            Help.ShowCommandHelp(command);
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
        Console.WriteLine($"Run '{AppName} {HelpCommand}' to see available commands.");
    }

    private void Init(string[] args)
    {
        var mode = ParseProjectMode(args);
        
        foreach (var worker in _fileWorkers)
            worker.Init(mode);
        
        var packageJsonPath = Path.Combine(Settings.WorkingDirectory, Settings.PackageJsonFile);
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
            Console.WriteLine($"Usage: {AppName} {AddPageCommand} <page-name>");
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
        
        // If the directory exists, delete it to ensure a clean demo
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }
        
        // First, run webstir init to create the base structure
        
        // Get the path to the current executable
        var webstirPath = Environment.ProcessPath ?? "dotnet";
        var webstirArgs = Environment.ProcessPath != null 
            ? $"{InitCommand} {targetDirectory}"
            : $"run -- {InitCommand} {targetDirectory}";
            
        var initProcess = new ProcessStartInfo
        {
            FileName = webstirPath,
            Arguments = webstirArgs,
            UseShellExecute = false,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };
        
        using (var process = Process.Start(initProcess))
        {
            process?.WaitForExit();
        }
        
        // Then use DemoBuilder to overlay the demo files
        var demoBuilder = new DemoBuilder(_fileWorkers);
        demoBuilder.CreateTemplate(targetDirectory);
        
        // Start webstir in the demo directory
        
        var watchArgs = Environment.ProcessPath != null 
            ? $"{WatchCommand} {targetDirectory}"
            : $"run -- {WatchCommand} {targetDirectory}";
            
        var watchProcess = new ProcessStartInfo
        {
            FileName = webstirPath,
            Arguments = watchArgs,
            UseShellExecute = false,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };
        
        using var watchProc = Process.Start(watchProcess);
        watchProc?.WaitForExit();
    }
}