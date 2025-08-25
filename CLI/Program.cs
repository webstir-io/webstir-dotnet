using CLI;
using Engine;
using Engine.Servers;
using Engine.Services;
using Engine.Workflows;
using Engine.Workers;
using Engine.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;

var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

    ServiceCollection services = new();
    services.AddSingleton<IConfiguration>(configuration);
    services.AddLogging(builder => builder.AddSerilog(logger)); 
    services.Configure<AppSettings>(options =>
    {
        var section = configuration.GetSection(nameof(AppSettings));
        if (section.Exists())
            section.Bind(options);
    });

    services.AddSingleton<Runner>();
    services.AddSingleton<WatchService>();
    services.AddSingleton<ChangeService>();
    services.AddSingleton<DevService>();
    services.AddSingleton<WebServer>();
    services.AddSingleton<NodeServer>();    
    services.AddSingleton(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });

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
    logger.Error(ex, "Fatal error occurred");
}