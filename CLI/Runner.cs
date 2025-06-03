using System.Diagnostics;

namespace CLI;

public class Runner(IEnumerable<IWebFileWorker> _webFileWorkers, Watcher _watcher)
{
    public void Run(string[] args)
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
                Add();
                break;

            case "build":
                Build();
                break;

            case "":
            case "watch":
                Build();
                Watch();
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
        foreach (var worker in _webFileWorkers)
            worker.Init();
    }

    public void Add()
    {
        //TODO: Rework this to be more user friendly, add a command router
        Console.Write("Enter the name of the page: ");
        var pageName = Console.ReadLine();
        if (string.IsNullOrEmpty(pageName))
        {
            Console.WriteLine("Page name must be provided.");
            return;
        }
        var pagePath = Directories.PagesDirectory.Join(pageName);
        var pageDirectory = Directory.CreateDirectory(pagePath);

        Console.Write($"Adding {pageName}...");
        foreach (var worker in _webFileWorkers)
            worker.Add(pageDirectory);     

        Console.WriteLine("Done");
    }

    public void Build(bool releaseMode = false)
    {
        Console.Write("Building...");

        if (Directory.Exists(Directories.BuildDirectory.FullName))
        {
            foreach (var directory in Directories.BuildDirectory.GetDirectories())
                directory.Delete(true);
        }

        foreach (var worker in _webFileWorkers.OrderBy(p => p.BuildOrder))
            worker.Build(releaseMode);

        Console.WriteLine("Done");
    }

    public void Publish()
    {
        Build(true);

        Console.Write("Publishing...");

        Directories.DistDirectory.Delete(true);

        foreach (var worker in _webFileWorkers)
            worker.Publish();
            
        Console.WriteLine("Done");
    }

    public void Watch()
    {
        _watcher.Watch(Build);
    }
}