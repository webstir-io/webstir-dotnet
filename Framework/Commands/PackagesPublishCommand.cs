using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Framework.Packaging;
using Framework.Services;
using Microsoft.Extensions.Logging;

namespace Framework.Commands;

internal sealed class PackagesPublishCommand(
    PackagesBumpCommand bumpCommand,
    PackagesSyncCommand syncCommand,
    IPackagePublishValidator publishValidator,
    IReleaseNotesService releaseNotesService,
    IPackageOperationReporter operationReporter,
    ILogger<PackagesPublishCommand> logger) : IPackagesSubcommand
{
    private readonly PackagesBumpCommand _bumpCommand = bumpCommand;
    private readonly PackagesSyncCommand _syncCommand = syncCommand;
    private readonly IPackagePublishValidator _publishValidator = publishValidator;
    private readonly IReleaseNotesService _releaseNotesService = releaseNotesService;
    private readonly IPackageOperationReporter _operationReporter = operationReporter;
    private readonly ILogger<PackagesPublishCommand> _logger = logger;

    public string Name => "publish";

    public IReadOnlyCollection<string> Aliases { get; } = new[] { "push" };

    public async Task<int> ExecuteAsync(PackagesCommandContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[packages] Starting publish pipeline...");

        PackageBumpSummary bumpSummary = await _bumpCommand.RunAsync(context, cancellationToken).ConfigureAwait(false);

        try
        {
            if (!bumpSummary.HasPackages)
            {
                await _operationReporter
                    .ReportAsync(
                        context,
                        bumpSummary,
                        PackageBuildSummary.None(context.IsDryRun, publish: true),
                        ReleaseNotesResult.Empty,
                        failureMessage: null,
                        cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation("[packages] Publish pipeline completed (no matching packages).");
                return 0;
            }

            LogBumpSummary(bumpSummary);

            if (context.IsDryRun)
            {
                _logger.LogInformation("[packages] (dry run) Skipping registry validation and publish.");
                PackageBuildSummary dryRunSummary = await _syncCommand.RunAsync(context, publish: true, cancellationToken).ConfigureAwait(false);
                LogBuildSummary(dryRunSummary);

                await _operationReporter
                    .ReportAsync(
                        context,
                        bumpSummary,
                        dryRunSummary,
                        ReleaseNotesResult.Empty,
                        failureMessage: null,
                        cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation("[packages] (dry run) Skipping release note generation.");
                _logger.LogInformation("[packages] (dry run) Publish pipeline completed.");
                return 0;
            }

            await _publishValidator
                .ValidateAsync(context.RepositoryRoot, context.Selection, context.SinceReference, cancellationToken)
                .ConfigureAwait(false);

            PackageBuildSummary buildSummary = await _syncCommand.RunAsync(context, publish: true, cancellationToken).ConfigureAwait(false);
            LogBuildSummary(buildSummary);

            ReleaseNotesResult notes = await _releaseNotesService
                .WriteAsync(context.RepositoryRoot, bumpSummary, cancellationToken)
                .ConfigureAwait(false);

            await _operationReporter
                .ReportAsync(
                    context,
                    bumpSummary,
                    buildSummary,
                    notes,
                    failureMessage: null,
                    cancellationToken)
                .ConfigureAwait(false);

            LogReleaseNotes(notes);
            _logger.LogInformation("[packages] Publish pipeline completed.");
            return 0;
        }
        catch (Exception ex)
        {
            await _operationReporter
                .ReportAsync(
                    context,
                    bumpSummary,
                    PackageBuildSummary.None(context.IsDryRun, publish: true),
                    ReleaseNotesResult.Empty,
                    ex.Message,
                    cancellationToken)
                .ConfigureAwait(false);
            throw;
        }
    }

    private void LogBumpSummary(PackageBumpSummary summary)
    {
        if (!summary.HasPackages || summary.TargetVersion is null)
        {
            return;
        }

        if (summary.ExplicitVersionSpecified)
        {
            _logger.LogInformation(
                "[packages] Set version {Version} for {Count} package(s).",
                summary.TargetVersion,
                summary.Entries.Count);
        }
        else if (summary.AppliedBump.HasValue)
        {
            _logger.LogInformation(
                "[packages] Applied {Bump} bump to {Count} package(s). Target version: {Version}.",
                summary.AppliedBump.Value,
                summary.Entries.Count,
                summary.TargetVersion);
        }

        foreach (PackageBumpEntry entry in summary.Entries)
        {
            _logger.LogInformation(
                "[packages]  {Package}: {Current} -> {Target}.",
                entry.PackageName,
                entry.CurrentVersion,
                summary.TargetVersion);

            if (summary.UsedHeuristics && entry.CommitMessages.Count > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                string sample = string.Join("; ", entry.CommitMessages.Take(3));
                _logger.LogDebug("[packages]     commits: {Commits}", sample);
            }
        }
    }

    private void LogBuildSummary(PackageBuildSummary summary)
    {
        if (!summary.HasPackages)
        {
            return;
        }

        if (summary.DryRun)
        {
            if (summary.PlannedPackages.Count > 0)
            {
                _logger.LogInformation(
                    "[packages] Planned to publish package(s): {Packages}.",
                    string.Join(", ", summary.PlannedPackages));
            }

            return;
        }

        foreach (PackageBuildResult result in summary.Results)
        {
            _logger.LogInformation(
                result.Published
                    ? "[packages]  Published {Package}@{Version} (registry specifier: {Registry})."
                    : "[packages]  Skipped publishing {Package}@{Version}; registry specifier: {Registry}.",
                result.PackageName,
                result.Version,
                result.RegistrySpecifier);
        }
    }

    private void LogReleaseNotes(ReleaseNotesResult result)
    {
        if (!result.HasDocuments)
        {
            _logger.LogInformation("[packages] No release notes generated (no notable commits detected).");
            return;
        }

        foreach (ReleaseNotesDocument document in result.Documents)
        {
            _logger.LogInformation(
                "[packages]  Release notes ready for {Package}@{Version} at {Path}.",
                document.PackageName,
                document.Version,
                document.FilePath);
        }
    }
}
