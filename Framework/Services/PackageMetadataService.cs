using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Framework.Utilities;
using Microsoft.Extensions.Logging;

namespace Framework.Services;

internal interface IPackageMetadataService
{
    Task<IReadOnlyList<PackageManifest>> GetPackagesAsync(string repositoryRoot, CancellationToken cancellationToken);

    Task<IReadOnlyList<PackageManifest>> ResolveAsync(
        string repositoryRoot,
        PackageSelection selection,
        string? sinceReference,
        CancellationToken cancellationToken);

    Task UpdatePackageVersionAsync(PackageManifest manifest, SemanticVersion version, bool dryRun, CancellationToken cancellationToken);
}

internal sealed class PackageMetadataService(
    IRepositoryDiffService repositoryDiffService,
    ILogger<PackageMetadataService> logger) : IPackageMetadataService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly IRepositoryDiffService _repositoryDiffService = repositoryDiffService;
    private readonly ILogger<PackageMetadataService> _logger = logger;

    public async Task<IReadOnlyList<PackageManifest>> GetPackagesAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        List<PackageManifest> manifests = new();
        foreach (PackageDefinition definition in PackageDefinition.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string packageDirectory = Path.Combine(repositoryRoot, definition.RelativePath);
            if (!Directory.Exists(packageDirectory))
            {
                _logger.LogDebug("Skipping package '{Key}' because directory '{Directory}' does not exist.", definition.Key, packageDirectory);
                continue;
            }

            PackageManifest manifest = await LoadManifestAsync(definition, packageDirectory, cancellationToken).ConfigureAwait(false);
            manifests.Add(manifest);
        }

        return manifests;
    }

    public async Task<IReadOnlyList<PackageManifest>> ResolveAsync(
        string repositoryRoot,
        PackageSelection selection,
        string? sinceReference,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PackageManifest> manifests = await GetPackagesAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PackageManifest> enabled = manifests
            .Where(manifest => manifest.IsEnabled)
            .OrderBy(manifest => manifest.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return selection.Mode switch
        {
            PackageSelectionMode.All => enabled,
            PackageSelectionMode.Explicit => ResolveExplicitSelection(manifests, selection),
            _ => await ResolveChangedSelectionAsync(enabled, repositoryRoot, sinceReference, cancellationToken).ConfigureAwait(false)
        };
    }

    public async Task UpdatePackageVersionAsync(PackageManifest manifest, SemanticVersion version, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        string versionText = version.ToString();

        // If the version is already current, skip file writes and keep logs quiet
        if (string.Equals(manifest.Version.ToString(), versionText, StringComparison.Ordinal))
        {
            _logger.LogDebug("[packages] {Package} already at {Version}; skipping.", manifest.PackageName, versionText);
            return;
        }
        await UpdatePackageJsonAsync(manifest.PackageJsonPath, versionText, dryRun, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(manifest.PackageLockPath) && File.Exists(manifest.PackageLockPath))
        {
            await UpdatePackageLockAsync(manifest.PackageLockPath!, versionText, dryRun, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogDebug(
            dryRun ? "[packages] (dry run) {Package} would be set to {Version}." : "[packages] {Package} set to {Version}.",
            manifest.PackageName,
            versionText);
    }

    private static async Task<PackageManifest> LoadManifestAsync(PackageDefinition definition, string packageDirectory, CancellationToken cancellationToken)
    {
        string packageJsonPath = Path.Combine(packageDirectory, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            throw new FileNotFoundException($"package.json not found for package '{definition.Key}'", packageJsonPath);
        }

        await using FileStream packageStream = File.OpenRead(packageJsonPath);
        using JsonDocument packageDocument = await JsonDocument.ParseAsync(packageStream, cancellationToken: cancellationToken).ConfigureAwait(false);

        JsonElement root = packageDocument.RootElement;
        string packageName = root.GetProperty("name").GetString()
            ?? throw new InvalidOperationException($"package.json for '{definition.Key}' is missing a name.");
        string version = root.GetProperty("version").GetString()
            ?? throw new InvalidOperationException($"package.json for '{definition.Key}' is missing a version.");

        SemanticVersion semanticVersion = SemanticVersion.Parse(version);

        string packageLockPath = Path.Combine(packageDirectory, "package-lock.json");

        ImmutableHashSet<string>.Builder identifiers = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        identifiers.Add(definition.Key);
        foreach (string alias in definition.Aliases)
        {
            identifiers.Add(alias);
        }

        identifiers.Add(packageName);

        return new PackageManifest(
            definition.Key,
            packageName,
            packageDirectory,
            packageJsonPath,
            File.Exists(packageLockPath) ? packageLockPath : null,
            semanticVersion,
            identifiers.ToImmutable(),
            definition.IsEnabled);
    }

    private static IReadOnlyList<PackageManifest> ResolveExplicitSelection(IReadOnlyList<PackageManifest> manifests, PackageSelection selection)
    {
        List<PackageManifest> resolved = new();
        HashSet<string> missing = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> disabled = new(StringComparer.OrdinalIgnoreCase);

        foreach (string identifier in selection.Identifiers)
        {
            PackageManifest? match = manifests.FirstOrDefault(manifest => manifest.MatchesIdentifier(identifier));
            if (match is null)
            {
                missing.Add(identifier);
                continue;
            }

            if (!match.IsEnabled)
            {
                disabled.Add(match.PackageName);
                continue;
            }

            if (!resolved.Contains(match))
            {
                resolved.Add(match);
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Unknown package identifier(s): {string.Join(", ", missing)}");
        }

        if (disabled.Count > 0)
        {
            throw new InvalidOperationException($"Package(s) disabled and unavailable: {string.Join(", ", disabled)}");
        }

        return resolved;
    }

    private async Task<IReadOnlyList<PackageManifest>> ResolveChangedSelectionAsync(
        IReadOnlyList<PackageManifest> manifests,
        string repositoryRoot,
        string? sinceReference,
        CancellationToken cancellationToken)
    {
        RepositoryDiffOptions options = new(sinceReference, IncludeUntracked: sinceReference is null);
        RepositoryDiffResult diff = await _repositoryDiffService.GetStatusAsync(repositoryRoot, options, cancellationToken).ConfigureAwait(false);

        if (!diff.HasChanges)
        {
            _logger.LogInformation("[packages] No repository changes detected; use --all to target every package.");
            return Array.Empty<PackageManifest>();
        }

        HashSet<PackageManifest> changed = new();

        foreach (string relativePath in diff.Paths)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            string absolutePath = ResolveAbsolutePath(repositoryRoot, relativePath);
            foreach (PackageManifest manifest in manifests)
            {
                if (IsPathUnderDirectory(absolutePath, manifest.PackageDirectory))
                {
                    changed.Add(manifest);
                }
            }
        }

        if (changed.Count == 0)
        {
            // Nothing actionable for framework packages; a no-op is expected when only docs/infra changed.
            _logger.LogDebug(
                "[packages] Ignoring {Count} changed path(s) that do not map to framework packages.",
                diff.Paths.Count);
            return Array.Empty<PackageManifest>();
        }

        string packageList = string.Join(", ", changed.Select(manifest => manifest.PackageName));
        _logger.LogInformation(
            "[packages] Detected changes for {Count} package(s): {Packages}.",
            changed.Count,
            packageList);

        return changed
            .OrderBy(manifest => manifest.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveAbsolutePath(string repositoryRoot, string relativePath)
    {
        string normalized = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalized))
        {
            return Path.GetFullPath(normalized);
        }

        string combined = Path.Combine(repositoryRoot, normalized);
        return Path.GetFullPath(combined);
    }

    private static bool IsPathUnderDirectory(string path, string directory)
    {
        string relative = Path.GetRelativePath(directory, path);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static async Task UpdatePackageJsonAsync(string path, string version, bool dryRun, CancellationToken cancellationToken)
    {
        JsonNode? root = await ReadJsonAsync(path, cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            throw new InvalidOperationException($"Unable to parse JSON file '{path}'.");
        }

        root["version"] = version;

        if (!dryRun)
        {
            await WriteJsonAsync(path, root, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task UpdatePackageLockAsync(string path, string version, bool dryRun, CancellationToken cancellationToken)
    {
        JsonNode? root = await ReadJsonAsync(path, cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            throw new InvalidOperationException($"Unable to parse JSON file '{path}'.");
        }

        root["version"] = version;

        if (root["packages"] is JsonObject packages && packages[string.Empty] is JsonObject rootPackage)
        {
            rootPackage["version"] = version;
        }

        if (!dryRun)
        {
            await WriteJsonAsync(path, root, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<JsonNode?> ReadJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync(string path, JsonNode json, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, json, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record PackageDefinition(string Key, IReadOnlyCollection<string> Aliases, string RelativePath, bool IsEnabled)
    {
        internal static readonly IReadOnlyList<PackageDefinition> All =
        [
            new PackageDefinition(
                "frontend",
                new[] { "frontend", "@webstir-io/webstir-frontend" },
                Path.Combine("Framework", "Frontend"),
                IsEnabled: true),
            new PackageDefinition(
                "testing",
                new[] { "testing", "test", "@webstir-io/webstir-testing" },
                Path.Combine("Framework", "Testing"),
                IsEnabled: true),
            new PackageDefinition(
                "backend",
                new[] { "backend", "@webstir-io/webstir-backend" },
                Path.Combine("Framework", "Backend"),
                IsEnabled: true)
        ];
    }
}

internal sealed record PackageManifest(
    string Key,
    string PackageName,
    string PackageDirectory,
    string PackageJsonPath,
    string? PackageLockPath,
    SemanticVersion Version,
    IReadOnlySet<string> Identifiers,
    bool IsEnabled)
{
    public bool MatchesIdentifier(string identifier) => Identifiers.Contains(identifier, StringComparer.OrdinalIgnoreCase);
}

internal sealed record PackageSelection(PackageSelectionMode Mode, IReadOnlyList<string> Identifiers)
{
    public static PackageSelection AllPackages { get; } = new(PackageSelectionMode.All, Array.Empty<string>());

    public static PackageSelection ChangedPackages { get; } = new(PackageSelectionMode.Changed, Array.Empty<string>());

    public static PackageSelection Explicit(IReadOnlyList<string> identifiers) => new(PackageSelectionMode.Explicit, identifiers);

    public bool IsChanged => Mode == PackageSelectionMode.Changed;

    public bool IsExplicit => Mode == PackageSelectionMode.Explicit;

    public bool IsAll => Mode == PackageSelectionMode.All;
}

internal enum PackageSelectionMode
{
    All,
    Changed,
    Explicit
}
