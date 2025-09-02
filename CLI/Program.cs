using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;

using CLI;
using Engine;
using Engine.Pipelines.Assets;
using Engine.Pipelines.Css;
using Engine.Pipelines.Css.Build;
using Engine.Pipelines.Css.Publish;
using Engine.Pipelines.Html;
using Engine.Pipelines.Html.Build;
using Engine.Pipelines.Html.Publish;
using Engine.Pipelines.JavaScript;
using Engine.Pipelines.JavaScript.Build;
using Engine.Pipelines.JavaScript.Publish;
using Engine.Servers;
using Engine.Services;
using Engine.Workers;
using Engine.Workflows;

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
    services.AddTransient<HtmlHandler>();
    services.AddTransient<HtmlBuilder>();
    services.AddTransient<HtmlBundler>();
    services.AddTransient<CssHandler>();
    services.AddTransient<JsHandler>();
    services.AddTransient<AssetHandler>();

    services.AddTransient<CssBuilder>();
    services.AddTransient<JsBuilder>();
    services.AddTransient<CssBundler>();
    services.AddTransient<JsBundler>();

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
