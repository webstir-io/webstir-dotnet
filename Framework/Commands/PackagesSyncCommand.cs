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
            _logger.LogWarning("[packages] No framework packages matched the selection.");
            return PackageBuildSummary.None(context.IsDryRun, publish);
        }

        _logger.LogInformation(
            publish ? "[packages] Publishing {Count} package(s)..." : "[packages] Building {Count} package(s)...",
            manifests.Count);

        if (publish)
        {
            if (context.PruneWebstir)
            {
                _logger.LogWarning("[packages] --prune-webstir is ignored during publish.");
            }
        }

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

        if (!publish && context.PruneWebstir)
        {
            PruneWebstirDirectories(context.RepositoryRoot);
        }

        _logger.LogInformation("[packages] Done.");
        return PackageBuildSummary.FromResults(results, publish);
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
