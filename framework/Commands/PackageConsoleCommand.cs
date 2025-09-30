namespace Framework.Commands;

using System;
using System.IO;
using System.Threading.Tasks;
using Framework.Packaging;
using Microsoft.Extensions.Logging;

internal sealed class PackageConsoleCommand
{
    private const string SyncCommand = "sync";
    private const string PublishCommand = "publish";

    private readonly PackageBuilder _packageBuilder;
    private readonly ILogger<PackageConsoleCommand> _logger;

    public PackageConsoleCommand(PackageBuilder packageBuilder, ILogger<PackageConsoleCommand> logger)
    {
        _packageBuilder = packageBuilder;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            ParsedArguments parsed = ParseArguments(args);
            string repositoryRoot = Directory.GetCurrentDirectory();
            bool publishPackages = parsed.Mode == PublishCommand;

            if (publishPackages)
            {
                _logger.LogInformation("[packages] Publishing framework packages to the configured registry...");
                await _packageBuilder.ValidatePublishAsync(repositoryRoot);
            }
            else
            {
                _logger.LogInformation("[packages] Building framework packages...");
            }

            if (parsed.IncludeFrontend)
            {
                PackageBuildResult result = await _packageBuilder.BuildFrontendAsync(repositoryRoot, publishPackages);
                LogResult(result, publishPackages);
            }

            if (parsed.IncludeTesting)
            {
                PackageBuildResult result = await _packageBuilder.BuildTestingAsync(repositoryRoot, publishPackages);
                LogResult(result, publishPackages);
            }

            _logger.LogInformation("[packages] Done.");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "framework packages command failed.");
            return 1;
        }
    }

    private void LogResult(PackageBuildResult result, bool publish)
    {
        _logger.LogInformation(
            "[packages] Built {Package} {Version}. Tarball: {Tarball}.",
            result.PackageName,
            result.Version,
            result.TarballPath);

        if (publish)
        {
            if (result.Published)
            {
                _logger.LogInformation("[packages] Published {Package}@{Version} to registry.", result.PackageName, result.Version);
            }
            else
            {
                _logger.LogInformation("[packages] Skipped publishing {Package}@{Version}; version already exists or publish disabled.", result.PackageName, result.Version);
            }
        }
    }

    private static ParsedArguments ParseArguments(string[] args)
    {
        string mode = SyncCommand;
        bool includeFrontend = true;
        bool includeTesting = true;

        int index = 0;
        if (args.Length > 0 && !IsOption(args[0]))
        {
            mode = args[0].ToLowerInvariant();
            index = 1;
        }

        for (int i = index; i < args.Length; i++)
        {
            string current = args[i];
            switch (current)
            {
                case "--frontend":
                case "-f":
                    includeFrontend = true;
                    includeTesting = false;
                    break;
                case "--test":
                case "-t":
                    includeFrontend = false;
                    includeTesting = true;
                    break;
                case "--both":
                    includeFrontend = true;
                    includeTesting = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown packages option '{current}'.");
            }
        }

        if (mode is not SyncCommand and not PublishCommand)
        {
            throw new InvalidOperationException($"Unknown packages command '{mode}'. Use '{SyncCommand}' or '{PublishCommand}'.");
        }

        if (!includeFrontend && !includeTesting)
        {
            includeFrontend = true;
            includeTesting = true;
        }

        return new ParsedArguments(mode, includeFrontend, includeTesting);
    }

    private static bool IsOption(string value) => value.StartsWith("-", StringComparison.Ordinal);

    private readonly record struct ParsedArguments(string Mode, bool IncludeFrontend, bool IncludeTesting);
}
