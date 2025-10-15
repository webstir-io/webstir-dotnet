namespace Framework.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Framework.Services;
using Framework.Utilities;
using Microsoft.Extensions.Logging;

internal sealed class PackagesBumpCommand(
    IPackageMetadataService metadataService,
    IGitCommitAnalyzer commitAnalyzer,
    ILogger<PackagesBumpCommand> logger) : IPackagesSubcommand
{
    private readonly IPackageMetadataService _metadataService = metadataService;
    private readonly IGitCommitAnalyzer _commitAnalyzer = commitAnalyzer;
    private readonly ILogger<PackagesBumpCommand> _logger = logger;

    public string Name => "bump";

    public IReadOnlyCollection<string> Aliases { get; } = new[] { "version" };

    public async Task<int> ExecuteAsync(PackagesCommandContext context, CancellationToken cancellationToken)
    {
        await RunAsync(context, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    internal async Task<PackageBumpSummary> RunAsync(PackagesCommandContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<PackageManifest> manifests = await _metadataService
            .ResolveAsync(context.RepositoryRoot, context.Selection, context.SinceReference, cancellationToken)
            .ConfigureAwait(false);

        if (manifests.Count == 0)
        {
            _logger.LogWarning("[packages] No framework packages found to bump.");
            return PackageBumpSummary.None(context.IsDryRun);
        }

        SemanticVersion? explicitVersion = context.ExplicitVersion;
        SemanticVersion currentHighest = manifests[0].Version;
        foreach (PackageManifest manifest in manifests)
        {
            if (manifest.Version.CompareTo(currentHighest) > 0)
            {
                currentHighest = manifest.Version;
            }
        }

        List<PackageBumpEntry> entries = new(manifests.Count);
        SemanticVersion targetVersion;
        SemanticVersionBump? appliedBump = null;
        bool usedHeuristics = false;

        if (explicitVersion is not null)
        {
            targetVersion = explicitVersion.Value;
            _logger.LogInformation("[packages] Using explicit version {Version} for {Count} package(s).", targetVersion, manifests.Count);
            entries.AddRange(manifests.Select(manifest => PackageBumpEntry.Create(manifest, null, Array.Empty<string>())));
        }
        else if (context.BumpExplicit)
        {
            targetVersion = currentHighest.Increment(context.Bump);
            appliedBump = context.Bump;
            entries.AddRange(manifests.Select(manifest => PackageBumpEntry.Create(manifest, null, Array.Empty<string>())));
            _logger.LogInformation(
                "[packages] Bumping {Count} package(s) from {Current} to {Target} ({Bump}).",
                manifests.Count,
                currentHighest,
                targetVersion,
                context.Bump);
        }
        else
        {
            SemanticVersionBump highest = SemanticVersionBump.Patch;
            bool foundSignals = false;

            foreach (PackageManifest manifest in manifests)
            {
                CommitHistoryAnalysis analysis = await _commitAnalyzer
                    .AnalyzeAsync(context.RepositoryRoot, manifest, context.SinceReference, cancellationToken)
                    .ConfigureAwait(false);

                entries.Add(PackageBumpEntry.Create(manifest, analysis.SuggestedBump, analysis.CommitMessages));

                if (analysis.SuggestedBump.HasValue)
                {
                    foundSignals = true;
                    highest = Max(highest, analysis.SuggestedBump.Value);
                }
            }

            if (foundSignals)
            {
                usedHeuristics = true;
                appliedBump = highest;
                targetVersion = currentHighest.Increment(highest);
                _logger.LogInformation(
                    "[packages] Conventional commit analysis selected a {Bump} bump from {Current} to {Target}.",
                    highest,
                    currentHighest,
                    targetVersion);
            }
            else
            {
                appliedBump = SemanticVersionBump.Patch;
                targetVersion = currentHighest.Increment(appliedBump.Value);
                _logger.LogInformation(
                    "[packages] Defaulting to patch bump: {Current} -> {Target}.",
                    currentHighest,
                    targetVersion);
            }
        }

        foreach (PackageManifest manifest in manifests)
        {
            await _metadataService.UpdatePackageVersionAsync(manifest, targetVersion, context.IsDryRun, cancellationToken).ConfigureAwait(false);
        }

        if (context.IsDryRun)
        {
            _logger.LogInformation(
                "[packages] (dry run) Would update {Count} package(s) to version {Version}.",
                manifests.Count,
                targetVersion);
        }
        else
        {
            _logger.LogInformation(
                "[packages] Updated {Count} package(s) to version {Version}.",
                manifests.Count,
                targetVersion);
        }

        return new PackageBumpSummary(
            HasPackages: true,
            TargetVersion: targetVersion,
            AppliedBump: context.HasExplicitVersion ? null : appliedBump,
            UsedHeuristics: usedHeuristics,
            ExplicitVersionSpecified: context.HasExplicitVersion,
            Entries: entries,
            DryRun: context.IsDryRun);
    }

    private static SemanticVersionBump Max(SemanticVersionBump left, SemanticVersionBump right)
    {
        if (left == SemanticVersionBump.Major || right == SemanticVersionBump.Major)
        {
            return SemanticVersionBump.Major;
        }

        if (left == SemanticVersionBump.Minor || right == SemanticVersionBump.Minor)
        {
            return SemanticVersionBump.Minor;
        }

        return SemanticVersionBump.Patch;
    }
}

internal sealed record PackageBumpSummary(
    bool HasPackages,
    SemanticVersion? TargetVersion,
    SemanticVersionBump? AppliedBump,
    bool UsedHeuristics,
    bool ExplicitVersionSpecified,
    IReadOnlyList<PackageBumpEntry> Entries,
    bool DryRun)
{
    public static PackageBumpSummary None(bool dryRun) =>
        new(false, null, null, UsedHeuristics: false, ExplicitVersionSpecified: false, Array.Empty<PackageBumpEntry>(), dryRun);
}

internal sealed record PackageBumpEntry(
    string PackageName,
    SemanticVersion CurrentVersion,
    SemanticVersionBump? SuggestedBump,
    IReadOnlyList<string> CommitMessages)
{
    public static PackageBumpEntry Create(PackageManifest manifest, SemanticVersionBump? suggestedBump, IReadOnlyList<string> commitMessages) =>
        new(manifest.PackageName, manifest.Version, suggestedBump, commitMessages);
}
