namespace Framework.Commands;

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Framework.Packaging;
using Microsoft.Extensions.Logging;

internal sealed class PackageConsoleCommand(PackageBuilder packageBuilder, ILogger<PackageConsoleCommand> logger)
{
    private const string SyncCommand = "sync";
    private const string PublishCommand = "publish";
    private const string VerifyCommand = "verify";
    private const string DiffCommand = "diff";
    private const string PruneWebstirOption = "--prune-webstir";

    private readonly PackageBuilder _packageBuilder = packageBuilder;
    private readonly ILogger<PackageConsoleCommand> _logger = logger;

    public async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            ParsedArguments parsed = ParseArguments(args);
            string repositoryRoot = Directory.GetCurrentDirectory();
            bool publishPackages = parsed.Mode == PublishCommand;
            bool verifyPackages = parsed.Mode == VerifyCommand;
            bool diffPackages = parsed.Mode == DiffCommand;

            if (verifyPackages)
            {
                if (parsed.PruneWebstir)
                {
                    _logger.LogWarning("[packages] {Option} is ignored when running verify.", PruneWebstirOption);
                }
                _logger.LogInformation("[packages] Verifying framework packages...");
                await _packageBuilder.VerifyAsync(repositoryRoot, parsed.IncludeFrontend, parsed.IncludeTesting);
                _logger.LogInformation("[packages] Verification succeeded.");
                return 0;
            }

            if (diffPackages)
            {
                if (parsed.PruneWebstir)
                {
                    _logger.LogWarning("[packages] {Option} is ignored when running diff.", PruneWebstirOption);
                }

                _logger.LogInformation("[packages] Calculating package tarball diffs...");
                PackageDiffSummary summary = await _packageBuilder.DiffAsync(repositoryRoot, parsed.IncludeFrontend, parsed.IncludeTesting);
                foreach (PackageDiffEntry entry in summary.Entries)
                {
                    switch (entry.State)
                    {
                        case PackageDiffState.Unchanged:
                            _logger.LogInformation("[packages] {Package} tarball matches embedded metadata.", entry.PackageName);
                            break;
                        case PackageDiffState.Changed:
                            string actualSizeText = entry.ActualSize.HasValue
                                ? entry.ActualSize.Value.ToString(CultureInfo.InvariantCulture)
                                : "(n/a)";

                            _logger.LogWarning(
                                "[packages] {Package} tarball differs (expected {ExpectedSha} / {ExpectedSize} bytes, found {ActualSha} / {ActualSize} bytes).",
                                entry.PackageName,
                                entry.ExpectedSha,
                                entry.ExpectedSize,
                                entry.ActualSha ?? "(n/a)",
                                actualSizeText);
                            if (!string.IsNullOrWhiteSpace(entry.Message))
                            {
                                _logger.LogWarning("[packages] {Message}", entry.Message);
                            }
                            break;
                        case PackageDiffState.Missing:
                            _logger.LogWarning("[packages] {Package} tarball missing: {Message}.", entry.PackageName, entry.Message);
                            break;
                    }
                }

                if (summary.HasChanges)
                {
                    _logger.LogWarning("[packages] Differences detected. Run 'framework packages sync' to regenerate tarballs.");
                    return 1;
                }

                _logger.LogInformation("[packages] All package tarballs match recorded metadata.");
                return 0;
            }

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

            if (parsed.PruneWebstir)
            {
                if (publishPackages)
                {
                    _logger.LogWarning("[packages] {Option} is ignored during publish.", PruneWebstirOption);
                }
                else
                {
                    PruneWebstirDirectories(repositoryRoot);
                }
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
            result.Tarball.RepositoryPath);

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
        bool pruneWebstir = false;

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
                case PruneWebstirOption:
                    pruneWebstir = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown packages option '{current}'.");
            }
        }

        if (mode is not SyncCommand and not PublishCommand and not VerifyCommand and not DiffCommand)
        {
            throw new InvalidOperationException($"Unknown packages command '{mode}'. Use '{SyncCommand}', '{PublishCommand}', '{VerifyCommand}', or '{DiffCommand}'.");
        }

        if (!includeFrontend && !includeTesting)
        {
            includeFrontend = true;
            includeTesting = true;
        }

        return new ParsedArguments(mode, includeFrontend, includeTesting, pruneWebstir);
    }

    private static bool IsOption(string value) => value.StartsWith("-", StringComparison.Ordinal);

    private void PruneWebstirDirectories(string repositoryRoot)
    {
        string[] roots =
        {
            Path.Combine(repositoryRoot, "Tests", "out"),
            Path.Combine(repositoryRoot, "CLI", "out")
        };

        int totalRemoved = 0;

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string directory in Directory.EnumerateDirectories(root, ".webstir", SearchOption.AllDirectories))
            {
                int removed = 0;
                foreach (string file in Directory.EnumerateFiles(directory, "*.tgz", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(file);
                        removed++;
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "[packages] Unable to delete tarball {File}.", file);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning(ex, "[packages] Insufficient permissions to delete tarball {File}.", file);
                    }
                }

                if (removed > 0)
                {
                    _logger.LogInformation("[packages] Pruned {Count} cached tarball(s) from {Directory}.", removed, directory);
                    totalRemoved += removed;
                }
            }
        }

        if (totalRemoved == 0)
        {
            _logger.LogInformation("[packages] No cached .webstir tarballs found to prune.");
        }
    }

    private readonly record struct ParsedArguments(string Mode, bool IncludeFrontend, bool IncludeTesting, bool PruneWebstir);
}
