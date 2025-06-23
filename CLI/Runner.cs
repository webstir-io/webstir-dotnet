using CLI.Interfaces;

namespace CLI;

public class Runner(IEnumerable<IWebFileWorker> _webFileWorkers, Watcher _watcher, INodeServer _nodeServer)
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
                Build();
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
        foreach (var worker in _webFileWorkers)
            worker.Init();
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
        foreach (var worker in _webFileWorkers)
            worker.Add(pageDirectory);     

        Console.WriteLine($"✓ Created page at {pagePath}");
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

    public async Task Watch()
    {
        // Start Node.js server if server build exists
        await _nodeServer.StartAsync();
        
        // Setup cleanup on exit
        Console.CancelKeyPress += async (sender, e) =>
        {
            e.Cancel = true;
            await _nodeServer.StopAsync();
            Environment.Exit(0);
        };
        
        await _watcher.Watch(Build);
    }
}