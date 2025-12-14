using System;
using System.Globalization;
using System.Text.Json;
using CLI;
using Engine;
using Engine.Bridge.Backend;
using Engine.Bridge.Frontend;
using Engine.Bridge.Shared;
using Engine.Servers;
using Engine.Services;
using Engine.Workflows;
using Engine.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Utilities.Process;

Logger logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    IConfigurationRoot configuration = new ConfigurationBuilder()
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

    Logger configuredLogger = new LoggerConfiguration()
        .ReadFrom.Configuration(configuration)
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .CreateLogger();

    logger.Dispose();
    logger = configuredLogger;

    ServiceCollection services = new();
    services.AddSingleton<IConfiguration>(configuration);
    services.AddLogging(builder => builder.AddSerilog(logger, dispose: true));
    services.Configure<AppSettings>(options =>
    {
        IConfigurationSection section = configuration.GetSection(nameof(AppSettings));
        if (section.Exists())
        {
            section.Bind(options);
        }
    });

    services.AddSingleton<Runner>();
    services.AddSingleton<WatchService>();
    services.AddSingleton<ChangeService>();
    services.AddSingleton<DevService>();
    services.AddSingleton<WebServer>();
    services.AddSingleton<NodeServer>();
    services.AddSingleton<ErrorTrackingService>();
    services.AddSingleton(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });
    services.AddSingleton<IProcessRunner, ProcessRunner>();

    services.AddScoped<AppWorkspace>();
    services.AddScoped<IWorkflowFactory, WorkflowFactory>();

    services.AddTransient<IWorkflowWorker, FrontendWorker>();
    services.AddTransient<IWorkflowWorker, BackendWorker>();
    services.AddTransient<IWorkflowWorker, SharedWorker>();

    services.AddTransient<IWorkflow, InitWorkflow>();
    services.AddTransient<IWorkflow, RepairWorkflow>();
    services.AddTransient<IWorkflow, BuildWorkflow>();
    services.AddTransient<IWorkflow, PublishWorkflow>();
    services.AddTransient<IWorkflow, InstallWorkflow>();
    services.AddTransient<IWorkflow, AddPageWorkflow>();
    services.AddTransient<IWorkflow, AddTestWorkflow>();
    services.AddTransient<IWorkflow, AddRouteWorkflow>();
    services.AddTransient<IWorkflow, AddJobWorkflow>();
    services.AddTransient<IWorkflow, EnableWorkflow>();
    services.AddTransient<IWorkflow, TestWorkflow>();
    services.AddTransient<IWorkflow, WatchWorkflow>();
    services.AddTransient<IWorkflow, SmokeWorkflow>();
    services.AddTransient<IWorkflow, BackendInspectWorkflow>();

    using ServiceProvider provider = services.BuildServiceProvider();
    await provider.GetRequiredService<Runner>().Run(args);
}
catch (Exception ex)
{
    logger.Error(ex, "Fatal error occurred");
    Environment.ExitCode = 1;
}
finally
{
    logger.Dispose();
}
