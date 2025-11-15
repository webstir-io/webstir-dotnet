using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Framework.Packaging;
using Framework.Services;
using Microsoft.Extensions.Logging;

namespace Framework.Commands;

internal sealed class PackagesSyncCommand(PackageBuilder packageBuilder, IPackageMetadataService metadataService, ILogger<PackagesSyncCommand> logger)
    : IPackagesSubcommand
{
    private readonly PackageBuilder _packageBuilder = packageBuilder;
    private readonly IPackageMetadataService _metadataService = metadataService;
    private readonly ILogger<PackagesSyncCommand> _logger = logger;

    public string Name => "sync";

    public IReadOnlyCollection<string> Aliases { get; } = new[] { "build" };

    public async Task<int> ExecuteAsync(PackagesCommandContext context, CancellationToken cancellationToken)
    {
        await RunAsync(context, publish: false, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    internal async Task<int> ExecutePublishAsync(PackagesCommandContext context, CancellationToken cancellationToken)
    {
        await RunAsync(context, publish: true, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    internal async Task<PackageBuildSummary> RunAsync(PackagesCommandContext context, bool publish, CancellationToken cancellationToken)
    {
        string action = publish ? "Publishing" : "Building";
        string actionVerb = publish ? "publish" : "build";
        IReadOnlyList<PackageManifest> manifests = await _metadataService
            .ResolveAsync(context.RepositoryRoot, context.Selection, context.SinceReference, cancellationToken)
            .ConfigureAwait(false);

        if (manifests.Count == 0)
        {
            _logger.LogInformation(
                "[packages] No framework package changes detected; nothing to sync. Use '--all' to rebuild all packages.");
            return PackageBuildSummary.None(context.IsDryRun, publish);
        }

        _logger.LogInformation(
            publish ? "[packages] Publishing {Count} package(s)..." : "[packages] Building {Count} package(s)...",
            manifests.Count);

        if (context.IsDryRun)
        {
            List<string> planned = new(manifests.Count);
            foreach (PackageManifest manifest in manifests)
            {
                planned.Add(manifest.PackageName);
                _logger.LogInformation(
                    "[packages] (dry run) Would {Action} {Package} from {Directory}.",
                    actionVerb,
                    manifest.PackageName,
                    manifest.PackageDirectory);
            }

            return PackageBuildSummary.FromDryRun(planned, publish);
        }

        List<PackageBuildResult> results = new(manifests.Count);
        foreach (PackageManifest manifest in manifests)
        {
            PackageBuildResult result = manifest.Key switch
            {
                "frontend" => await _packageBuilder.BuildFrontendAsync(context.RepositoryRoot, publish).ConfigureAwait(false),
                "testing" => await _packageBuilder.BuildTestingAsync(context.RepositoryRoot, publish).ConfigureAwait(false),
                "backend" => await _packageBuilder.BuildBackendAsync(context.RepositoryRoot, publish).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unsupported package key '{manifest.Key}'.")
            };

            results.Add(result);
            LogResult(result, publish);
        }

        _logger.LogInformation("[packages] Done.");
        return PackageBuildSummary.FromResults(results, publish);
    }

    private void LogResult(PackageBuildResult result, bool publish)
    {
        _logger.LogInformation(
            "[packages] Built {Package} {Version}.",
            result.PackageName,
            result.Version);

        if (publish)
        {
            if (result.Published)
            {
                _logger.LogInformation(
                    "[packages] Published {Package}@{Version} to registry.",
                    result.PackageName,
                    result.Version);
            }
            else
            {
                _logger.LogInformation(
                    "[packages] Skipped publishing {Package}@{Version}; version already exists or publish disabled.",
                    result.PackageName,
                    result.Version);
            }
        }
    }
}

internal sealed record PackageBuildSummary(
    bool HasPackages,
    bool Publish,
    bool DryRun,
    IReadOnlyList<string> PlannedPackages,
    IReadOnlyList<PackageBuildResult> Results)
{
    public static PackageBuildSummary None(bool dryRun, bool publish) =>
        new(false, publish, dryRun, Array.Empty<string>(), Array.Empty<PackageBuildResult>());

    public static PackageBuildSummary FromDryRun(IReadOnlyList<string> planned, bool publish) =>
        new(true, publish, DryRun: true, planned, Array.Empty<PackageBuildResult>());

    public static PackageBuildSummary FromResults(IReadOnlyList<PackageBuildResult> results, bool publish) =>
        new(true, publish, DryRun: false, Array.Empty<string>(), results);
}
