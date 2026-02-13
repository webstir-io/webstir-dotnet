using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Utilities.Process;

namespace Framework.Packaging;

public sealed class PackageBuilder
{
    private readonly ILogger<PackageBuilder> _logger;
    private readonly IProcessRunner _processRunner;

    public PackageBuilder(ILogger<PackageBuilder> logger, IProcessRunner processRunner)
    {
        _logger = logger;
        _processRunner = processRunner;
    }

    public async Task<PackageBuildResult> BuildFrontendAsync(string repositoryRoot, bool publish) =>
        await BuildAsync(repositoryRoot, FrameworkPackageDescriptor.Frontend, publish).ConfigureAwait(false);

    public async Task<PackageBuildResult> BuildTestingAsync(string repositoryRoot, bool publish) =>
        await BuildAsync(repositoryRoot, FrameworkPackageDescriptor.Testing, publish).ConfigureAwait(false);

    public async Task<PackageBuildResult> BuildBackendAsync(string repositoryRoot, bool publish) =>
        await BuildAsync(repositoryRoot, FrameworkPackageDescriptor.Backend, publish).ConfigureAwait(false);

    public Task VerifyAsync(string repositoryRoot, bool includeFrontend, bool includeTesting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        JsonObject packagesNode = LoadManifestPackages(repositoryRoot);

        List<string> failures = new();

        if (includeFrontend)
        {
            failures.AddRange(VerifyPackage(repositoryRoot, FrameworkPackageDescriptor.Frontend, packagesNode));
        }

        if (includeTesting)
        {
            failures.AddRange(VerifyPackage(repositoryRoot, FrameworkPackageDescriptor.Testing, packagesNode));
        }

        failures.AddRange(VerifyTemplateDependencies(repositoryRoot, packagesNode, includeFrontend, includeTesting));
        failures.AddRange(VerifyTarballResourcesRemoved(repositoryRoot));

        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                _logger.LogError("[packages] {Failure}", failure);
            }

            throw new InvalidOperationException("Framework package verification failed.");
        }

        _logger.LogDebug("[packages] Registry metadata verification succeeded.");
        return Task.CompletedTask;
    }

    public Task<PackageDiffSummary> DiffAsync(string repositoryRoot, bool includeFrontend, bool includeTesting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        JsonObject packagesNode = LoadManifestPackages(repositoryRoot);

        List<PackageDiffEntry> entries = new();

        if (includeFrontend)
        {
            entries.Add(DiffPackage(repositoryRoot, FrameworkPackageDescriptor.Frontend, packagesNode));
        }

        if (includeTesting)
        {
            entries.Add(DiffPackage(repositoryRoot, FrameworkPackageDescriptor.Testing, packagesNode));
        }

        return Task.FromResult(new PackageDiffSummary(entries));
    }

    private async Task<PackageBuildResult> BuildAsync(string repositoryRoot, FrameworkPackageDescriptor descriptor, bool publishPackages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        string packageDirectory = Path.Combine(repositoryRoot, descriptor.PackageRelativePath);
        if (!Directory.Exists(packageDirectory))
        {
            throw new DirectoryNotFoundException($"Package directory not found: {packageDirectory}");
        }

        string packageJsonPath = Path.Combine(packageDirectory, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            throw new FileNotFoundException($"package.json not found for {descriptor.PackageName} at {packageJsonPath}");
        }

        PackageMetadata metadata = LoadPackageMetadata(packageJsonPath, descriptor);

        await RunCommandAsync("npm", "ci --silent", packageDirectory, $"npm ci ({descriptor.PackageName})").ConfigureAwait(false);
        await RunCommandAsync("npm", "run build --silent", packageDirectory, $"npm run build ({descriptor.PackageName})").ConfigureAwait(false);

        string workspaceSpecifier = ResolveWorkspaceSpecifier(descriptor, metadata);
        string registrySpecifier = ResolveRegistrySpecifier(descriptor, metadata);

        bool published = false;
        if (publishPackages && descriptor.SupportsPublishing && !string.IsNullOrWhiteSpace(descriptor.PublishRegistryUrl))
        {
            string spec = descriptor.GetPackageSpec(metadata.Version);
            published = await PublishToRegistryAsync(spec, descriptor.PublishRegistryUrl!, packageDirectory, descriptor.PublishAccess).ConfigureAwait(false);
        }

        UpdatePackageCatalog(repositoryRoot, metadata.PackageName, metadata.Version, workspaceSpecifier, registrySpecifier);
        UpdateEngineResourcesPackageJson(Path.Combine(repositoryRoot, "Engine", "Resources", "package.json"), metadata.PackageName, workspaceSpecifier, metadata.Version);

        CleanupPackageDirectory(packageDirectory, descriptor.CleanupDirectories);
        RemoveLegacyTarballResources(repositoryRoot);

        return new PackageBuildResult(metadata.PackageName, metadata.Version, workspaceSpecifier, registrySpecifier, published);
    }

    private static PackageMetadata LoadPackageMetadata(string packageJsonPath, FrameworkPackageDescriptor descriptor)
    {
        using FileStream stream = File.OpenRead(packageJsonPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        string version = document.RootElement.GetProperty("version").GetString() ?? throw new InvalidOperationException($"Package version missing for {descriptor.PackageName}");
        string name = document.RootElement.GetProperty("name").GetString() ?? descriptor.PackageName;
        return new PackageMetadata(name, version);
    }

    private async Task RunCommandAsync(string fileName, string arguments, string workingDirectory, string description)
    {
        ProcessSpec spec = new()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            ExitTimeout = TimeSpan.FromMinutes(10)
        };

        try
        {
            ProcessResult result = await _processRunner.RunAsync(spec).ConfigureAwait(false);

            if (!result.CompletedSuccessfully)
            {
                StringBuilder builder = new();
                builder.AppendLine(FormattableString.Invariant($"Command '{description}' failed with exit code {result.ExitCode}."));
                if (!string.IsNullOrWhiteSpace(result.StandardError))
                {
                    builder.AppendLine(result.StandardError.Trim());
                }
                if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    builder.AppendLine(result.StandardOutput.Trim());
                }

                throw new InvalidOperationException(builder.ToString().Trim());
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Unable to execute '{fileName}'. Ensure it is installed and available on the PATH.", ex);
        }
    }

    private async Task<bool> PublishToRegistryAsync(string spec, string registryUrl, string packageDirectory, string publishAccess)
    {
        if (string.IsNullOrWhiteSpace(registryUrl))
        {
            return false;
        }

        if (await PackageExistsAsync(spec, registryUrl, packageDirectory).ConfigureAwait(false))
        {
            _logger.LogInformation("[packages] {Spec} already exists in {Registry}.", spec, registryUrl);
            return false;
        }

        ProcessSpec specPublish = new()
        {
            FileName = "npm",
            Arguments = $"publish --registry \"{registryUrl}\" --access {publishAccess}",
            WorkingDirectory = packageDirectory,
            ExitTimeout = TimeSpan.FromMinutes(10)
        };

        ProcessResult publishResult = await _processRunner.RunAsync(specPublish).ConfigureAwait(false);

        if (!publishResult.CompletedSuccessfully)
        {
            if (!string.IsNullOrWhiteSpace(publishResult.StandardError) && publishResult.StandardError.IndexOf("EPUBLISHCONFLICT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _logger.LogInformation("[packages] {Spec} already exists in {Registry}.", spec, registryUrl);
                return false;
            }

            StringBuilder builder = new();
            builder.AppendLine(FormattableString.Invariant($"npm publish failed for {spec} (exit {publishResult.ExitCode})."));
            if (!string.IsNullOrWhiteSpace(publishResult.StandardError))
            {
                builder.AppendLine(publishResult.StandardError.Trim());
            }
            if (!string.IsNullOrWhiteSpace(publishResult.StandardOutput))
            {
                builder.AppendLine(publishResult.StandardOutput.Trim());
            }

            throw new InvalidOperationException(builder.ToString().Trim());
        }

        _logger.LogInformation("[packages] Published {Spec} to {Registry}.", spec, registryUrl);
        return true;
    }

    private async Task<bool> PackageExistsAsync(string spec, string registryUrl, string workingDirectory)
    {
        ProcessSpec specView = new()
        {
            FileName = "npm",
            Arguments = $"view {spec} version --registry \"{registryUrl}\"",
            WorkingDirectory = workingDirectory,
            ExitTimeout = TimeSpan.FromSeconds(30)
        };

        ProcessResult result = await _processRunner.RunAsync(specView).ConfigureAwait(false);
        return result.CompletedSuccessfully;
    }

    private static void UpdatePackageCatalog(
        string repositoryRoot,
        string packageName,
        string version,
        string workspaceSpecifier,
        string registrySpecifier)
    {
        string catalogPath = Path.Combine(repositoryRoot, "Framework", "Packaging", "framework-packages.json");
        JsonObject root = File.Exists(catalogPath)
            ? JsonNode.Parse(File.ReadAllText(catalogPath)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        JsonObject packages = root["packages"] as JsonObject ?? new JsonObject();

        JsonObject packageNode = new()
        {
            ["name"] = packageName,
            ["version"] = version,
            ["registrySpecifier"] = registrySpecifier
        };

        if (!string.IsNullOrWhiteSpace(workspaceSpecifier))
        {
            packageNode["workspaceSpecifier"] = workspaceSpecifier;
        }

        packages[packageName] = packageNode;

        JsonObject orderedPackages = new();
        foreach (KeyValuePair<string, JsonNode?> item in packages.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            orderedPackages[item.Key] = item.Value?.DeepClone();
        }

        root["packages"] = orderedPackages;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
        File.WriteAllText(catalogPath, root.ToJsonString(options) + Environment.NewLine);
    }

    private static void UpdateEngineResourcesPackageJson(string packageJsonPath, string packageName, string workspaceSpecifier, string version)
    {
        string dependencyValue = !string.IsNullOrWhiteSpace(workspaceSpecifier)
            ? workspaceSpecifier
            : "^" + version;

        JsonObject root = File.Exists(packageJsonPath)
            ? JsonNode.Parse(File.ReadAllText(packageJsonPath)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        JsonObject dependencies = root["dependencies"] as JsonObject ?? new JsonObject();
        dependencies[packageName] = dependencyValue;
        root["dependencies"] = dependencies;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(Path.GetDirectoryName(packageJsonPath)!);
        File.WriteAllText(packageJsonPath, root.ToJsonString(options) + Environment.NewLine);
    }

    private static void CleanupPackageDirectory(string packageDirectory, IReadOnlyCollection<string> directoriesToRemove)
    {
        foreach (string directoryName in directoriesToRemove)
        {
            string path = Path.Combine(packageDirectory, directoryName);
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                }
                catch (IOException)
                {
                    // ignore cleanup issues
                }
                catch (UnauthorizedAccessException)
                {
                    // ignore cleanup issues
                }
            }
        }
    }

    private static void RemoveLegacyTarballResources(string repositoryRoot)
    {
        string resourcesPath = Path.Combine(repositoryRoot, "Framework", "Resources", "webstir");
        if (!Directory.Exists(resourcesPath))
        {
            return;
        }

        bool removedAny = false;

        foreach (string file in Directory.EnumerateFiles(resourcesPath, "*.tgz", SearchOption.TopDirectoryOnly))
        {
            try
            {
                File.Delete(file);
                removedAny = true;
            }
            catch (IOException)
            {
                // ignore cleanup issues
            }
            catch (UnauthorizedAccessException)
            {
                // ignore cleanup issues
            }
        }

        if (removedAny && !Directory.EnumerateFileSystemEntries(resourcesPath).Any())
        {
            try
            {
                Directory.Delete(resourcesPath);
            }
            catch
            {
                // best effort
            }
        }
    }

    private static JsonObject LoadManifestPackages(string repositoryRoot)
    {
        string catalogPath = Path.Combine(repositoryRoot, "Framework", "Packaging", "framework-packages.json");
        if (!File.Exists(catalogPath))
        {
            throw new FileNotFoundException($"Framework package catalog not found at {catalogPath}");
        }

        if (JsonNode.Parse(File.ReadAllText(catalogPath)) is not JsonObject root)
        {
            throw new InvalidOperationException("Framework package catalog is not a JSON object.");
        }

        if (root["packages"] is not JsonObject packages)
        {
            throw new InvalidOperationException("Framework package catalog missing 'packages' element.");
        }

        return packages;
    }

    private static PackageDiffEntry DiffPackage(
        string repositoryRoot,
        FrameworkPackageDescriptor descriptor,
        JsonObject packagesNode)
    {
        if (packagesNode[descriptor.PackageName] is not JsonObject packageNode)
        {
            return PackageDiffEntry.Missing(descriptor.PackageName, "Package metadata missing from framework-packages.json.");
        }

        string recordedVersion = packageNode["version"]?.GetValue<string>() ?? string.Empty;
        string recordedRegistrySpecifier = packageNode["registrySpecifier"]?.GetValue<string>() ?? string.Empty;
        string recordedWorkspaceSpecifier = packageNode["workspaceSpecifier"]?.GetValue<string>() ?? string.Empty;

        string packageDirectory = Path.Combine(repositoryRoot, descriptor.PackageRelativePath);
        if (!Directory.Exists(packageDirectory))
        {
            return PackageDiffEntry.Missing(descriptor.PackageName, $"Package directory not found: {packageDirectory}");
        }

        string packageJsonPath = Path.Combine(packageDirectory, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return PackageDiffEntry.Missing(descriptor.PackageName, $"package.json not found for {descriptor.PackageName} at {packageJsonPath}");
        }

        PackageMetadata metadata = LoadPackageMetadata(packageJsonPath, descriptor);
        string expectedRegistrySpecifier = ResolveRegistrySpecifier(descriptor, metadata);
        string expectedWorkspaceSpecifier = ResolveWorkspaceSpecifier(descriptor, metadata);

        bool versionMatches = string.Equals(metadata.Version, recordedVersion, StringComparison.Ordinal);
        bool registryMatches = string.Equals(recordedRegistrySpecifier, expectedRegistrySpecifier, StringComparison.Ordinal);
        bool workspaceMatches = string.IsNullOrWhiteSpace(expectedWorkspaceSpecifier) ||
            string.Equals(recordedWorkspaceSpecifier, expectedWorkspaceSpecifier, StringComparison.Ordinal);

        if (versionMatches && registryMatches && workspaceMatches)
        {
            return PackageDiffEntry.Unchanged(descriptor.PackageName, recordedVersion);
        }

        List<string> messages = new();
        if (!versionMatches)
        {
            messages.Add($"version mismatch (recorded {recordedVersion}, actual {metadata.Version})");
        }
        if (!registryMatches)
        {
            messages.Add("registry specifier drift");
        }
        if (!workspaceMatches)
        {
            messages.Add("workspace specifier drift");
        }

        return PackageDiffEntry.Changed(
            descriptor.PackageName,
            recordedVersion,
            metadata.Version,
            recordedRegistrySpecifier,
            expectedRegistrySpecifier,
            recordedWorkspaceSpecifier,
            expectedWorkspaceSpecifier,
            string.Join("; ", messages));
    }

    private static IEnumerable<string> VerifyPackage(
        string repositoryRoot,
        FrameworkPackageDescriptor descriptor,
        JsonObject packagesNode)
    {
        List<string> failures = new();

        if (packagesNode[descriptor.PackageName] is not JsonObject packageNode)
        {
            failures.Add($"Package '{descriptor.PackageName}' missing from framework-packages.json.");
            return failures;
        }

        string recordedVersion = packageNode["version"]?.GetValue<string>() ?? string.Empty;
        string recordedRegistrySpecifier = packageNode["registrySpecifier"]?.GetValue<string>() ?? string.Empty;
        string recordedWorkspaceSpecifier = packageNode["workspaceSpecifier"]?.GetValue<string>() ?? string.Empty;

        string packageDirectory = Path.Combine(repositoryRoot, descriptor.PackageRelativePath);
        if (!Directory.Exists(packageDirectory))
        {
            failures.Add($"Package directory not found: {packageDirectory}");
            return failures;
        }

        string packageJsonPath = Path.Combine(packageDirectory, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            failures.Add($"package.json not found for {descriptor.PackageName} at {packageJsonPath}");
            return failures;
        }

        PackageMetadata metadata = LoadPackageMetadata(packageJsonPath, descriptor);
        string expectedRegistrySpecifier = ResolveRegistrySpecifier(descriptor, metadata);
        string expectedWorkspaceSpecifier = ResolveWorkspaceSpecifier(descriptor, metadata);

        if (!string.Equals(metadata.Version, recordedVersion, StringComparison.Ordinal))
        {
            failures.Add($"Package '{descriptor.PackageName}' version mismatch: recorded {recordedVersion}, found {metadata.Version}.");
        }

        if (!string.Equals(recordedRegistrySpecifier, expectedRegistrySpecifier, StringComparison.Ordinal))
        {
            failures.Add($"Package '{descriptor.PackageName}' registry specifier mismatch: recorded '{recordedRegistrySpecifier}', expected '{expectedRegistrySpecifier}'.");
        }

        if (!string.IsNullOrWhiteSpace(expectedWorkspaceSpecifier) &&
            !string.Equals(recordedWorkspaceSpecifier, expectedWorkspaceSpecifier, StringComparison.Ordinal))
        {
            failures.Add($"Package '{descriptor.PackageName}' workspace specifier mismatch: recorded '{recordedWorkspaceSpecifier}', expected '{expectedWorkspaceSpecifier}'.");
        }

        return failures;
    }

    private static IEnumerable<string> VerifyTemplateDependencies(
        string repositoryRoot,
        JsonObject packagesNode,
        bool includeFrontend,
        bool includeTesting)
    {
        string templatePackageJsonPath = Path.Combine(repositoryRoot, "Engine", "Resources", "package.json");
        if (!File.Exists(templatePackageJsonPath))
        {
            return new[] { $"Template package.json not found at {templatePackageJsonPath}." };
        }

        List<(string PackageName, string Expected)> expectations = new();

        if (TryGetCaretExpectation(packagesNode, FrameworkPackageDescriptor.Backend.PackageName, out string backendExpectation))
        {
            expectations.Add((FrameworkPackageDescriptor.Backend.PackageName, backendExpectation));
        }

        if (includeFrontend && TryGetCaretExpectation(packagesNode, FrameworkPackageDescriptor.Frontend.PackageName, out string frontendExpectation))
        {
            expectations.Add((FrameworkPackageDescriptor.Frontend.PackageName, frontendExpectation));
        }

        if (includeTesting && TryGetCaretExpectation(packagesNode, FrameworkPackageDescriptor.Testing.PackageName, out string testingExpectation))
        {
            expectations.Add((FrameworkPackageDescriptor.Testing.PackageName, testingExpectation));
        }

        List<string> failures = new();

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(templatePackageJsonPath));
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("dependencies", out JsonElement dependencies) || dependencies.ValueKind != JsonValueKind.Object)
        {
            failures.Add("Template package.json missing dependencies section.");
            return failures;
        }

        foreach ((string packageName, string expectedSpecifier) in expectations)
        {
            if (!dependencies.TryGetProperty(packageName, out JsonElement value))
            {
                failures.Add($"Template package.json missing dependency '{packageName}'.");
                continue;
            }

            string actual = value.GetString() ?? string.Empty;
            if (!actual.StartsWith("^", StringComparison.Ordinal))
            {
                failures.Add($"Template dependency '{packageName}' should use a caret range but found '{actual}'.");
                continue;
            }

            if (!string.Equals(actual, expectedSpecifier, StringComparison.Ordinal))
            {
                failures.Add($"Template dependency '{packageName}' mismatch: expected '{expectedSpecifier}', found '{actual}'.");
            }
        }

        return failures;
    }

    private static bool TryGetCaretExpectation(JsonObject packagesNode, string packageName, out string expectation)
    {
        expectation = string.Empty;
        if (packagesNode[packageName] is not JsonObject packageNode)
        {
            return false;
        }

        string version = packageNode["version"]?.GetValue<string>() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        expectation = "^" + version;
        return true;
    }

    private static IEnumerable<string> VerifyTarballResourcesRemoved(string repositoryRoot)
    {
        string resourcesPath = Path.Combine(repositoryRoot, "Framework", "Resources", "webstir");
        if (!Directory.Exists(resourcesPath))
        {
            return Array.Empty<string>();
        }

        List<string> failures = new();
        string[] tarballs = Directory.GetFiles(resourcesPath, "*.tgz", SearchOption.TopDirectoryOnly);
        if (tarballs.Length > 0)
        {
            failures.Add($"Legacy tarball resources found under {resourcesPath}. Remove the files: {string.Join(", ", tarballs.Select(Path.GetFileName))}.");
        }

        return failures;
    }

    private static string ResolveWorkspaceSpecifier(FrameworkPackageDescriptor descriptor, PackageMetadata metadata)
    {
        return descriptor.GetWorkspaceSpecifierOverride()
            ?? descriptor.GetDefaultWorkspaceSpecifier(metadata.Version)
            ?? "^" + metadata.Version;
    }

    private static string ResolveRegistrySpecifier(FrameworkPackageDescriptor descriptor, PackageMetadata metadata)
    {
        return GetRegistrySpecifier(descriptor.RegistrySpecifierEnvironmentVariable)
            ?? descriptor.GetDefaultRegistrySpecifier(metadata.Version)
            ?? descriptor.GetPackageSpec(metadata.Version);
    }

    private static string? GetRegistrySpecifier(string? environmentVariable)
    {
        if (string.IsNullOrWhiteSpace(environmentVariable))
        {
            return null;
        }

        string? value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private readonly record struct PackageMetadata(string PackageName, string Version);
}

public readonly record struct PackageBuildResult(
    string PackageName,
    string Version,
    string WorkspaceSpecifier,
    string RegistrySpecifier,
    bool Published);

public enum PackageDiffState
{
    Unchanged,
    Changed,
    Missing
}

public readonly record struct PackageDiffEntry(
    string PackageName,
    PackageDiffState State,
    string? Message,
    string? RecordedVersion,
    string? ActualVersion,
    string? RecordedRegistrySpecifier,
    string? ExpectedRegistrySpecifier,
    string? RecordedWorkspaceSpecifier,
    string? ExpectedWorkspaceSpecifier)
{
    public static PackageDiffEntry Unchanged(string packageName, string recordedVersion) =>
        new(packageName, PackageDiffState.Unchanged, null, recordedVersion, recordedVersion, null, null, null, null);

    public static PackageDiffEntry Changed(
        string packageName,
        string recordedVersion,
        string actualVersion,
        string recordedRegistrySpecifier,
        string expectedRegistrySpecifier,
        string recordedWorkspaceSpecifier,
        string expectedWorkspaceSpecifier,
        string message) =>
        new(packageName, PackageDiffState.Changed, message, recordedVersion, actualVersion, recordedRegistrySpecifier, expectedRegistrySpecifier, recordedWorkspaceSpecifier, expectedWorkspaceSpecifier);

    public static PackageDiffEntry Missing(string packageName, string message) =>
        new(packageName, PackageDiffState.Missing, message, null, null, null, null, null, null);
}

public sealed class PackageDiffSummary
{
    public PackageDiffSummary(IReadOnlyList<PackageDiffEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<PackageDiffEntry> Entries
    {
        get;
    }

    public bool HasChanges => Entries.Any(entry => entry.State != PackageDiffState.Unchanged);
}
