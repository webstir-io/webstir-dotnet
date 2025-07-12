using CLI;
using Engine;
using Engine.Interfaces;
using Engine.Services;
using Engine.Workers;
using Engine.Workers.Client;
using Microsoft.Extensions.DependencyInjection;

try
{
    ServiceCollection services = new();
    services.AddSingleton<Runner>();
    services.AddSingleton<Watcher>();
    services.AddSingleton<IWebServer, WebServer>();
    services.AddSingleton<INodeServer, NodeServer>();

    services.AddTransient<IFileWorker, MarkupWorker>();
    services.AddTransient<IFileWorker, StylesWorker>();
    services.AddTransient<IFileWorker, ScriptsWorker>();
    services.AddTransient<IFileWorker, ImagesWorker>();
    services.AddTransient<IFileWorker, ServerWorker>();
    services.AddTransient<IFileWorker, SharedWorker>();

    using ServiceProvider provider = services.BuildServiceProvider();
    await provider.GetService<Runner>()!.Run(args);

}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}; Stack: {ex.StackTrace}");
}