using System;
using System.Globalization;
using Framework;
using Framework.Commands;
using Framework.Packaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;

Logger logger = CreateBootstrapLogger();

try
{
    IConfigurationRoot configuration = BuildConfiguration();
    Logger configuredLogger = BuildLogger(configuration);
    logger.Dispose();
    logger = configuredLogger;

    ServiceCollection services = new();
    services.AddSingleton<IConfiguration>(configuration);
    services.AddLogging(builder => builder.AddSerilog(logger, dispose: true));
    services.AddSingleton<PackageBuilder>();
    services.AddSingleton<PackageConsoleCommand>();
    services.AddSingleton<Runner>();

    await using ServiceProvider provider = services.BuildServiceProvider();
    Runner commandRouter = provider.GetRequiredService<Runner>();
    return await commandRouter.ExecuteAsync(args);
}
catch (Exception ex)
{
    logger.Error(ex, "framework console failed.");
    return 1;
}
finally
{
    logger.Dispose();
}

static Logger CreateBootstrapLogger()
{
    return new LoggerConfiguration()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .CreateLogger();
}

static IConfigurationRoot BuildConfiguration()
{
    return new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();
}

static Logger BuildLogger(IConfiguration configuration)
{
    return new LoggerConfiguration()
        .ReadFrom.Configuration(configuration)
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
        .CreateLogger();
}
