using CLI;
using Engine;
using Engine.Servers;
using Engine.Modules;
using Engine.Services;
using Engine.Workers;
using Engine.Workers.Client;
using Engine.Workers.Shared;
using Engine.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Engine.Workers.Server;

try
{
    ServiceCollection services = new();
    services.AddSingleton<Runner>();
    services.AddSingleton<WatchService>();
    services.AddSingleton<IWebServer, WebServer>();
    services.AddSingleton<INodeServer, NodeServer>();

    services.AddScoped<App>();
    services.AddScoped<IWorkflowFactory, WorkflowFactory>();

    services.AddTransient<IAppModule, ClientModule>();
    services.AddTransient<IAppModule, ServerModule>();
    services.AddTransient<IAppModule, SharedModule>();

    services.AddTransient<IModuleWorker, HtmlWorker>();
    services.AddTransient<IModuleWorker, StylesWorker>();
    services.AddTransient<IModuleWorker, ScriptsWorker>();
    services.AddTransient<IModuleWorker, ImagesWorker>();
    services.AddTransient<IModuleWorker, ServerWorker>();
    services.AddTransient<IModuleWorker, SharedWorker>();

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