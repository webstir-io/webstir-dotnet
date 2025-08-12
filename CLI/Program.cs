using CLI;
using Engine;
using Engine.Servers;
using Engine.Modules;
using Engine.Services;
using Engine.Workers;
using Engine.Workers.Client;
using Engine.Workers.Shared;
using Engine.Workflows;
using Engine.Workers.Server;
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

    services.AddTransient<IAppModule, ClientModule>();
    services.AddTransient<IAppModule, ServerModule>();
    services.AddTransient<IAppModule, SharedModule>();

    services.AddTransient<IClientWorker, HtmlWorker>();
    services.AddTransient<IClientWorker, StylesWorker>();
    services.AddTransient<IClientWorker, ScriptsWorker>();
    services.AddTransient<IClientWorker, ImagesWorker>();
    services.AddTransient<IServerWorker, ServerWorker>();
    services.AddTransient<ISharedWorker, SharedWorker>();

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