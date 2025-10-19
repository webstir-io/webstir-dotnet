using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Framework.Packaging;
using Framework.Services;
using Microsoft.Extensions.Logging;

namespace Framework.Commands;

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

        _logger.LogInformation("[packages] Checking framework package metadata...");
        PackageDiffSummary summary = await _packageBuilder
            .DiffAsync(context.RepositoryRoot, includeFrontend, includeTesting)
            .ConfigureAwait(false);

        foreach (PackageDiffEntry entry in summary.Entries)
        {
            switch (entry.State)
            {
                case PackageDiffState.Unchanged:
                    _logger.LogInformation("[packages] {Package} metadata matches recorded version {Version}.", entry.PackageName, entry.RecordedVersion ?? "(unknown)");
                    break;
                case PackageDiffState.Changed:
                    _logger.LogWarning("[packages] {Package} metadata drift detected: {Message}", entry.PackageName, entry.Message ?? "differences found.");
                    if (!string.Equals(entry.RecordedVersion, entry.ActualVersion, StringComparison.Ordinal))
                    {
                        _logger.LogWarning("[packages]   Version: recorded {Recorded}, actual {Actual}.", entry.RecordedVersion ?? "(missing)", entry.ActualVersion ?? "(missing)");
                    }
                    if (!string.Equals(entry.RecordedRegistrySpecifier, entry.ExpectedRegistrySpecifier, StringComparison.Ordinal))
                    {
                        _logger.LogWarning("[packages]   Registry specifier: recorded '{Recorded}', expected '{Expected}'.", entry.RecordedRegistrySpecifier ?? "(missing)", entry.ExpectedRegistrySpecifier ?? "(missing)");
                    }
                    if (!string.Equals(entry.RecordedWorkspaceSpecifier, entry.ExpectedWorkspaceSpecifier, StringComparison.Ordinal))
                    {
                        _logger.LogWarning("[packages]   Workspace specifier: recorded '{Recorded}', expected '{Expected}'.", entry.RecordedWorkspaceSpecifier ?? "(missing)", entry.ExpectedWorkspaceSpecifier ?? "(missing)");
                    }
                    break;
                case PackageDiffState.Missing:
                    _logger.LogWarning("[packages] {Package} metadata missing: {Message}.", entry.PackageName, entry.Message ?? "no entry recorded.");
                    break;
            }
        }

        if (summary.HasChanges)
        {
            _logger.LogWarning("[packages] Differences detected. Run 'framework packages sync' to refresh metadata.");
            return 1;
        }

        _logger.LogInformation("[packages] Framework package metadata is up to date.");
        return 0;
    }
}
