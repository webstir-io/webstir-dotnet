using CLI.Helpers;
using CLI.Interfaces;

namespace CLI;

public enum ProjectMode
{
    Unknown,
    Legacy,
    ClientOnly,
    ServerOnly,
    Fullstack
}

public class Runner(IEnumerable<IFileWorker> _fileWorkers, Watcher _watcher)
{    
    public async Task Run(string[] args)
    {
        var command = args.Length != 0 
            ? args.First() 
            : string.Empty;

        switch (command)
        {
            case "init":
                Init();
                break;
            
            case "add":
                Add(args.Skip(1).ToArray());
                break;

            case "build":
                Build(cleanBuild: args.Contains("--clean"));
                break;

            case "":
            case "watch":
                Build();
                await Watch();
                break;

            case "publish":
                Publish();
                break;

            default:
                Console.WriteLine($"Unknown command '{command}'");
                Build();
                break;
        }
    }

    public void Init()
    {
        foreach (var worker in _fileWorkers)
            worker.Init();
        
        // Copy package.json if it doesn't exist
        var packageJsonPath = Path.Combine(Directory.GetCurrentDirectory(), Settings.PackageJsonFile);
        if (!File.Exists(packageJsonPath))
        {
            AssemblyHelpers.WriteResourceToFile("", Settings.PackageJsonFile, packageJsonPath);
        }
    }

    public void Add(string[] args)
    {
        var pageName = args.FirstOrDefault();   
        if (string.IsNullOrEmpty(pageName))
        {
            Console.WriteLine("Usage: webstir add <page-name>");
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
        foreach (var worker in _fileWorkers)
            worker.Add(pageDirectory);     

        Console.WriteLine($"✓ Created page at {pagePath}");
    }

    public void Build(bool releaseMode = false, bool cleanBuild = false)
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

    public void Publish()
    {
        Build(true);

        Console.Write("Publishing...");

        Directories.DistDirectory.Delete(true);

        foreach (var worker in _fileWorkers)
            worker.Publish();
            
        Console.WriteLine("Done");
    }

    public async Task Watch()
    {
        await _watcher.Watch(cleanBuild => Build(cleanBuild: cleanBuild));
    }
}