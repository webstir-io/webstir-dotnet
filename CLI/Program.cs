using CLI;
using Engine.Servers;
using Engine.Services;
using Engine.Workflows;
using Engine.Workers;
using Engine.Workers.Server;
using Engine.Workers.Shared;
using Engine.Handlers;
using Microsoft.Extensions.DependencyInjection;

try
{
    ServiceCollection services = new();
    services.AddSingleton<Runner>();
    services.AddSingleton<WatchService>();
    services.AddSingleton<IWebServer, WebServer>();
    services.AddSingleton<INodeServer, NodeServer>();

    services.AddScoped<Engine.AppContext>();
    services.AddScoped<IWorkflowFactory, WorkflowFactory>();

    // Register handlers
    services.AddTransient<HtmlHandler>();
    services.AddTransient<CssHandler>();
    services.AddTransient<ScriptsHandler>();
    services.AddTransient<ImagesHandler>();

    // Register workers
    services.AddTransient<ClientWorker>();
    services.AddTransient<ServerWorker>();
    services.AddTransient<SharedWorker>();

    services.AddTransient<IWorkflow, InitWorkflow>();
    services.AddTransient<IWorkflow, BuildWorkflow>();
    services.AddTransient<IWorkflow, PublishWorkflow>();
    services.AddTransient<IWorkflow, AddPageWorkflow>();

    using ServiceProvider provider = services.BuildServiceProvider();
    await provider.GetService<Runner>()!.Run(args);

}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}; Stack: {ex.StackTrace}");
}