using System;
using System.Globalization;
using System.Text.Json;
using CLI;
using Engine;
using Engine.Pipelines.Core.Interfaces;
using Engine.Pipelines.Css;
using Engine.Pipelines.Css.Build;
using Engine.Pipelines.Css.Publish;
using Engine.Pipelines.Fonts;
using Engine.Pipelines.Html;
using Engine.Pipelines.Html.Build;
using Engine.Pipelines.Html.Publish;
using Engine.Pipelines.Images;
using Engine.Pipelines.JavaScript;
using Engine.Pipelines.JavaScript.Build;
using Engine.Pipelines.JavaScript.Publish;
using Engine.Pipelines.Media;
using Engine.Servers;
using Engine.Services;
using Engine.Workers;
using Engine.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;

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

    ServiceCollection services = new();
    services.AddSingleton<IConfiguration>(configuration);
    services.AddLogging(builder => builder.AddSerilog(logger));
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
    services.AddSingleton(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });

    services.AddScoped<AppWorkspace>();
    services.AddScoped<IWorkflowFactory, WorkflowFactory>();
    services.AddTransient<HtmlBuilder>();
    services.AddTransient<HtmlBundler>();

    services.AddTransient<CssBuilder>();
    services.AddTransient<JsBuilder>();
    services.AddTransient<CssBundler>();
    services.AddTransient<JsBundler>();
    services.AddTransient<JsAssetsPublisher>();

    services.AddTransient<IFrontendHandler, HtmlHandler>();
    services.AddTransient<IFrontendHandler, CssHandler>();
    services.AddTransient<IFrontendHandler, JsHandler>();
    services.AddTransient<IFrontendHandler, ImagesHandler>();
    services.AddTransient<IFrontendHandler, FontsHandler>();
    services.AddTransient<IFrontendHandler, MediaHandler>();

    services.AddTransient<ClientWorker>();
    services.AddTransient<ServerWorker>();
    services.AddTransient<SharedWorker>();

    services.AddTransient<IWorkflow, InitWorkflow>();
    services.AddTransient<IWorkflow, BuildWorkflow>();
    services.AddTransient<IWorkflow, PublishWorkflow>();
    services.AddTransient<IWorkflow, AddPageWorkflow>();
    services.AddTransient<IWorkflow, AddTestWorkflow>();
    services.AddTransient<IWorkflow, TestWorkflow>();
    services.AddTransient<IWorkflow, WatchWorkflow>();

    using ServiceProvider provider = services.BuildServiceProvider();
    await provider.GetService<Runner>()!.Run(args);
}
catch (Exception ex)
{
    logger.Error(ex, "Fatal error occurred");
}
