using CLI;
using Engine;
using Engine.Servers;
using Engine.Services;
using Engine.Workflows;
using Engine.Workers;
using Engine.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

    ServiceCollection services = new();
    services.AddSingleton<IConfiguration>(configuration); 
    services.Configure<AppSettings>(options =>
    {
        var section = configuration.GetSection("AppSettings");
        if (section.Exists())
            section.Bind(options);
    });

    services.AddSingleton<Runner>();
    services.AddSingleton<WatchService>();
    services.AddSingleton<WebServer>();
    services.AddSingleton<NodeServer>();

    services.AddScoped<AppWorkspace>();
    services.AddScoped<IWorkflowFactory, WorkflowFactory>();

    services.AddTransient<HtmlHandler>();
    services.AddTransient<CssHandler>();
    services.AddTransient<ScriptsHandler>();
    services.AddTransient<ImagesHandler>();

    services.AddTransient<ClientWorker>();
    services.AddTransient<ServerWorker>();
    services.AddTransient<SharedWorker>();

    services.AddTransient<IWorkflow, InitWorkflow>();
    services.AddTransient<IWorkflow, BuildWorkflow>();
    services.AddTransient<IWorkflow, PublishWorkflow>();
    services.AddTransient<IWorkflow, AddPageWorkflow>();
    services.AddTransient<IWorkflow, WatchWorkflow>();

    using ServiceProvider provider = services.BuildServiceProvider();
    await provider.GetService<Runner>()!.Run(args);

}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}; Stack: {ex.StackTrace}");
}