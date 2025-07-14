using CLI;
using Engine;
using Engine.Interfaces;
using Engine.Modules;
using Engine.Servers;
using Engine.Services;
using Engine.Workers;
using Engine.Workers.Client;
using Engine.Workers.Shared;
using Engine.Workflows;
using Microsoft.Extensions.DependencyInjection;

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

    services.AddTransient<InitWorkflow>();
    services.AddTransient<BuildWorkflow>();
    services.AddTransient<PublishWorkflow>();
    services.AddTransient<AddPageWorkflow>();

    using ServiceProvider provider = services.BuildServiceProvider();
    await provider.GetService<Runner>()!.Run(args);

}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}; Stack: {ex.StackTrace}");
}