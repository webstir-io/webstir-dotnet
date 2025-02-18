using CLI;
using CLI.Workers;
using Microsoft.Extensions.DependencyInjection;

try
{
    ServiceCollection services = new();
    services.AddSingleton<Runner>();
    services.AddSingleton<Watcher>();
    services.AddSingleton<Server>();
    services.AddTransient<IWebFileWorker, MarkupWorker>();
    services.AddTransient<IWebFileWorker, StylesWorker>();
    services.AddTransient<IWebFileWorker, ScriptsWorker>();
    services.AddTransient<IWebFileWorker, ImagesWorker>();

    using ServiceProvider provider = services.BuildServiceProvider();
    provider.GetService<Runner>()!.Run(args);

}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}; Stack: {ex.StackTrace}");
}