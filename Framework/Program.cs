using System;
using System.Globalization;
using Framework;
using Framework.Commands;
using Framework.Packaging;
using Framework.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using SharedProcessRunner = Utilities.Process;

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
    services.AddSingleton<SharedProcessRunner.IProcessRunner, SharedProcessRunner.ProcessRunner>();
    services.AddSingleton<PackageBuilder>();
    services.AddSingleton<IRepositoryDiffService, RepositoryDiffService>();
    services.AddSingleton<IGitCommitAnalyzer, GitCommitAnalyzer>();
    services.AddSingleton<IPackageMetadataService, PackageMetadataService>();
    services.AddSingleton<IReleaseNotesService, ReleaseNotesService>();
    services.AddSingleton<IPackagePublishValidator, PackagePublishValidator>();
    services.AddSingleton<IPackageOperationReporter, PackageOperationReporter>();

    services.AddSingleton<PackagesBumpCommand>();
    services.AddSingleton<PackagesSyncCommand>();
    services.AddSingleton<PackagesReleaseCommand>();
    services.AddSingleton<PackagesPublishCommand>();
    services.AddSingleton<PackagesVerifyCommand>();
    services.AddSingleton<PackagesDiffCommand>();

    services.AddSingleton<IPackagesSubcommand>(provider => provider.GetRequiredService<PackagesBumpCommand>());
    services.AddSingleton<IPackagesSubcommand>(provider => provider.GetRequiredService<PackagesSyncCommand>());
    services.AddSingleton<IPackagesSubcommand>(provider => provider.GetRequiredService<PackagesReleaseCommand>());
    services.AddSingleton<IPackagesSubcommand>(provider => provider.GetRequiredService<PackagesPublishCommand>());
    services.AddSingleton<IPackagesSubcommand>(provider => provider.GetRequiredService<PackagesVerifyCommand>());
    services.AddSingleton<IPackagesSubcommand>(provider => provider.GetRequiredService<PackagesDiffCommand>());

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
