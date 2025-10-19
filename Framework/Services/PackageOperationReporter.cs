using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Framework.Commands;
using Framework.Packaging;
using Framework.Utilities;
using Microsoft.Extensions.Logging;

namespace Framework.Services;

internal interface IPackageOperationReporter
{
    Task ReportAsync(
        PackagesCommandContext context,
        PackageBumpSummary bumpSummary,
        PackageBuildSummary buildSummary,
        ReleaseNotesResult releaseNotes,
        string? failureMessage,
        CancellationToken cancellationToken);
}

internal sealed class PackageOperationReporter(
    IPackageMetadataService metadataService,
    ILogger<PackageOperationReporter> logger) : IPackageOperationReporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IPackageMetadataService _metadataService = metadataService;
    private readonly ILogger<PackageOperationReporter> _logger = logger;

    public async Task ReportAsync(
        PackagesCommandContext context,
        PackageBumpSummary bumpSummary,
        PackageBuildSummary buildSummary,
        ReleaseNotesResult releaseNotes,
        string? failureMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(releaseNotes);

        IReadOnlyList<PackageManifest> manifests = await _metadataService
            .GetPackagesAsync(context.RepositoryRoot, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, PackageBuildResult> buildResults = buildSummary.Results
            .ToDictionary(result => result.PackageName, StringComparer.OrdinalIgnoreCase);

        HashSet<string> planned = new(buildSummary.PlannedPackages, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, PackageBumpEntry> bumped = bumpSummary.Entries
            .ToDictionary(entry => entry.PackageName, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, ReleaseNotesDocument> notesByPackage = releaseNotes.Documents
            .ToDictionary(document => document.PackageName, StringComparer.OrdinalIgnoreCase);

        List<PackageOperationPackage> packages = new(manifests.Count);

        foreach (PackageManifest manifest in manifests.OrderBy(manifest => manifest.PackageName, StringComparer.OrdinalIgnoreCase))
        {
            bool targeted = bumped.ContainsKey(manifest.PackageName);

            bool hasBuildResult = buildResults.TryGetValue(manifest.PackageName, out PackageBuildResult buildMatch);
            PackageBuildResult? buildResult = hasBuildResult ? buildMatch : (PackageBuildResult?)null;

            ReleaseNotesDocument? releaseDocument = null;
            if (notesByPackage.TryGetValue(manifest.PackageName, out ReleaseNotesDocument? releaseMatch))
            {
                releaseDocument = releaseMatch;
            }

            string status = DetermineStatus(
                manifest.IsEnabled,
                targeted,
                buildSummary.Publish,
                context.IsDryRun || buildSummary.DryRun,
                planned.Contains(manifest.PackageName),
                buildResult);

            SemanticVersion? targetVersion = bumpSummary.TargetVersion;
            if (!targetVersion.HasValue && targeted && context.HasExplicitVersion)
            {
                targetVersion = context.ExplicitVersion;
            }

            string? targetVersionText = targeted && targetVersion.HasValue
                ? targetVersion.Value.ToString()
                : null;

            PackageBumpEntry? bumpEntry = targeted ? bumped[manifest.PackageName] : null;
            string previousVersion = bumpEntry?.CurrentVersion.ToString() ?? manifest.Version.ToString();

            string? workspaceSpecifier = buildResult.HasValue ? buildResult.Value.WorkspaceSpecifier : null;
            string? registrySpecifier = buildResult.HasValue ? buildResult.Value.RegistrySpecifier : null;
            bool? published = buildResult.HasValue ? buildResult.Value.Published : null;

            PackageOperationPackage packageTelemetry = new(
                manifest.PackageName,
                manifest.IsEnabled,
                status,
                previousVersion,
                targetVersionText,
                manifest.Version.ToString(),
                workspaceSpecifier,
                registrySpecifier,
                published,
                bumpEntry?.CommitMessages ?? Array.Empty<string>(),
                releaseDocument?.FilePath,
                planned.Contains(manifest.PackageName));

            packages.Add(packageTelemetry);

            _logger.LogInformation(
                "[packages][telemetry] {Json}",
                JsonSerializer.Serialize(packageTelemetry, SerializerOptions));
        }

        PackageOperationReport report = new(
            context.Command,
            buildSummary.Publish,
            context.IsDryRun || buildSummary.DryRun,
            context.SinceReference,
            context.Selection.Mode.ToString(),
            failureMessage,
            DateTimeOffset.UtcNow,
            packages,
            releaseNotes.Documents.Select(document => new PackageOperationReleaseNote(
                document.PackageName,
                document.Version,
                document.FilePath)).ToArray());

        string artifactsRoot = Path.Combine(context.RepositoryRoot, "artifacts");
        Directory.CreateDirectory(artifactsRoot);

        string summaryPath = Path.Combine(artifactsRoot, "packages-release-summary.json");
        await using FileStream stream = File.Open(summaryPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, report, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[packages] Summary written to {SummaryPath}.", GetRelativePath(context.RepositoryRoot, summaryPath));
    }

    private static string DetermineStatus(
        bool isEnabled,
        bool targeted,
        bool publish,
        bool dryRun,
        bool planned,
        PackageBuildResult? result)
    {
        if (!isEnabled)
        {
            return "disabled";
        }

        if (!targeted)
        {
            return "unchanged";
        }

        if (dryRun || planned)
        {
            return publish ? "planned-publish" : "planned-build";
        }

        if (result.HasValue)
        {
            if (publish)
            {
                return result.Value.Published ? "published" : "publish-skipped";
            }

            return "built";
        }

        return publish ? "publish-skipped" : "skipped";
    }

    private static string GetRelativePath(string repositoryRoot, string path)
    {
        string relative = Path.GetRelativePath(repositoryRoot, path);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private sealed record PackageOperationReport(
        string Command,
        bool Publish,
        bool DryRun,
        string? SinceReference,
        string SelectionMode,
        string? Failure,
        DateTimeOffset GeneratedAt,
        IReadOnlyList<PackageOperationPackage> Packages,
        IReadOnlyList<PackageOperationReleaseNote> ReleaseNotes);

    private sealed record PackageOperationPackage(
        string Name,
        bool Enabled,
        string Status,
        string PreviousVersion,
        string? TargetVersion,
        string CurrentVersion,
        string? WorkspaceSpecifier,
        string? RegistrySpecifier,
        bool? Published,
        IReadOnlyList<string> CommitMessages,
        string? ReleaseNotesPath,
        bool Planned);

    private sealed record PackageOperationReleaseNote(string Package, string Version, string Path);
}
