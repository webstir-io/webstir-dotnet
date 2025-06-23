using CLI;
using CLI.Interfaces;
using CLI.Services;
using CLI.Workers;
using Microsoft.Extensions.DependencyInjection;

try
{
    ServiceCollection services = new();
    services.AddSingleton<Runner>();
    services.AddSingleton<Watcher>();
    services.AddSingleton<Server>();
    services.AddSingleton<INodeServer, NodeServer>();

    services.AddTransient<IWebFileWorker, MarkupWorker>();
    services.AddTransient<IWebFileWorker, StylesWorker>();
    services.AddTransient<IWebFileWorker, ScriptsWorker>();
    services.AddTransient<IWebFileWorker, ImagesWorker>();

    using ServiceProvider provider = services.BuildServiceProvider();
    await provider.GetService<Runner>()!.Run(args);

}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}; Stack: {ex.StackTrace}");
}