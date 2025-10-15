namespace Framework.Commands;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Framework.Packaging;
using Framework.Services;
using Microsoft.Extensions.Logging;

internal sealed class PackagesDiffCommand(PackageBuilder packageBuilder, IPackageMetadataService metadataService, ILogger<PackagesDiffCommand> logger)
    : IPackagesSubcommand
{
    private readonly PackageBuilder _packageBuilder = packageBuilder;
    private readonly IPackageMetadataService _metadataService = metadataService;
    private readonly ILogger<PackagesDiffCommand> _logger = logger;

    public string Name => "diff";

    public IReadOnlyCollection<string> Aliases { get; } = new[] { "compare" };

    public async Task<int> ExecuteAsync(PackagesCommandContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<PackageManifest> manifests = await _metadataService
            .ResolveAsync(context.RepositoryRoot, context.Selection, context.SinceReference, cancellationToken)
            .ConfigureAwait(false);

        if (manifests.Count == 0)
        {
            _logger.LogWarning("[packages] No framework packages matched the selection.");
            return 0;
        }

        bool includeFrontend = manifests.Any(manifest => manifest.Key.Equals("frontend", StringComparison.OrdinalIgnoreCase));
        bool includeTesting = manifests.Any(manifest => manifest.Key.Equals("testing", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation("[packages] Calculating package tarball diffs...");
        PackageDiffSummary summary = await _packageBuilder
            .DiffAsync(context.RepositoryRoot, includeFrontend, includeTesting)
            .ConfigureAwait(false);

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
}
