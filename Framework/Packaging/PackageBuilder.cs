using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Framework.Packaging;

public sealed class PackageBuilder(ILogger<PackageBuilder> logger)
{
    private readonly ILogger<PackageBuilder> _logger = logger;

    public async Task<PackageBuildResult> BuildFrontendAsync(string repositoryRoot, bool publish) =>
        await BuildAsync(repositoryRoot, FrameworkPackageDescriptor.Frontend, publish);

    public async Task<PackageBuildResult> BuildTestingAsync(string repositoryRoot, bool publish) =>
        await BuildAsync(repositoryRoot, FrameworkPackageDescriptor.Testing, publish);

    public async Task<PackageBuildResult> BuildBackendAsync(string repositoryRoot, bool publish) =>
        await BuildAsync(repositoryRoot, FrameworkPackageDescriptor.Backend, publish);

    public Task VerifyAsync(string repositoryRoot, bool includeFrontend, bool includeTesting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        string catalogPath = Path.Combine(repositoryRoot, "Framework", "Packaging", "framework-packages.json");
        if (!File.Exists(catalogPath))
        {
            throw new FileNotFoundException($"Framework package catalog not found at {catalogPath}");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        if (!document.RootElement.TryGetProperty("packages", out JsonElement packagesElement) || packagesElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Framework package catalog missing 'packages' element.");
        }

        List<string> failures = [];
        Dictionary<string, FrameworkPackageTarballMetadata> tarballs = new(StringComparer.Ordinal);

        if (includeFrontend)
        {
            VerifyPackage(repositoryRoot, packagesElement, "@webstir-io/webstir-frontend", tarballs, failures);
        }

        if (includeTesting)
        {
            VerifyPackage(repositoryRoot, packagesElement, "@webstir-io/webstir-test", tarballs, failures);
        }

        VerifyTemplateDependencies(repositoryRoot, tarballs, failures);

        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                _logger.LogError("[packages] {Failure}", failure);
            }

            throw new InvalidOperationException("Framework package verification failed.");
        }

        _logger.LogInformation("[packages] Tarball verification succeeded.");
        return Task.CompletedTask;
    }

    public async Task<PackageDiffSummary> DiffAsync(string repositoryRoot, bool includeFrontend, bool includeTesting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        string catalogPath = Path.Combine(repositoryRoot, "Framework", "Packaging", "framework-packages.json");
        if (!File.Exists(catalogPath))
        {
            throw new FileNotFoundException($"Framework package catalog not found at {catalogPath}");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        if (!document.RootElement.TryGetProperty("packages", out JsonElement packagesElement) || packagesElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Framework package catalog missing 'packages' element.");
        }

        List<PackageDiffEntry> entries = new();

        if (includeFrontend)
        {
            entries.Add(await DiffPackageAsync(repositoryRoot, packagesElement, FrameworkPackageDescriptor.Frontend).ConfigureAwait(false));
        }

        if (includeTesting)
        {
            entries.Add(await DiffPackageAsync(repositoryRoot, packagesElement, FrameworkPackageDescriptor.Testing).ConfigureAwait(false));
        }

        return new PackageDiffSummary(entries);
    }

    private void VerifyPackage(
        string repositoryRoot,
        JsonElement packagesElement,
        string packageKey,
        IDictionary<string, FrameworkPackageTarballMetadata> tarballs,
        ICollection<string> failures)
    {
        if (!packagesElement.TryGetProperty(packageKey, out JsonElement packageElement) || packageElement.ValueKind != JsonValueKind.Object)
        {
            failures.Add($"Package '{packageKey}' missing from framework-packages.json.");
            return;
        }

        string packageName = packageElement.GetProperty("name").GetString() ?? packageKey;

        FrameworkPackageTarballMetadata tarball;
        try
        {
            tarball = ReadTarballMetadata(packageKey, packageElement);
        }
        catch (Exception ex)
        {
            failures.Add(ex.Message);
            return;
        }

        tarballs[packageName] = tarball;

        string repositoryTarballPath = Path.Combine(repositoryRoot, tarball.RepositoryPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(repositoryTarballPath))
        {
            failures.Add($"Tarball for {packageName} not found at {repositoryTarballPath}.");
        }
        else
        {
            FileInfo repositoryInfo = new(repositoryTarballPath);
            if (repositoryInfo.Length != tarball.Size)
            {
                failures.Add($"Tarball size mismatch for {packageName}: expected {tarball.Size} bytes but found {repositoryInfo.Length} bytes.");
            }
            else
            {
                string repositoryHash = ComputeSha256(repositoryTarballPath);
                if (!string.Equals(repositoryHash, tarball.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"Tarball hash mismatch for {packageName}: expected {tarball.Sha256} but found {repositoryHash}.");
                }
            }
        }

        string embeddedTarballPath = Path.Combine(repositoryRoot, "Framework", "Resources", "webstir", tarball.FileName);
        if (!File.Exists(embeddedTarballPath))
        {
            failures.Add($"Embedded tarball for {packageName} not found at {embeddedTarballPath}.");
        }
        else
        {
            FileInfo embeddedInfo = new(embeddedTarballPath);
            if (embeddedInfo.Length != tarball.Size)
            {
                failures.Add($"Embedded tarball size mismatch for {packageName}: expected {tarball.Size} bytes but found {embeddedInfo.Length} bytes.");
            }
            else
            {
                string embeddedHash = ComputeSha256(embeddedTarballPath);
                if (!string.Equals(embeddedHash, tarball.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"Embedded tarball hash mismatch for {packageName}: expected {tarball.Sha256} but found {embeddedHash}.");
                }
            }
        }
    }

    private static FrameworkPackageTarballMetadata ReadTarballMetadata(string packageKey, JsonElement packageElement)
    {
        if (!packageElement.TryGetProperty("tarball", out JsonElement tarballElement) || tarballElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Package '{packageKey}' missing tarball metadata.");
        }

        string fileName = tarballElement.GetProperty("fileName").GetString()
            ?? throw new InvalidOperationException($"Package '{packageKey}' tarball missing fileName.");
        string repositoryPath = tarballElement.GetProperty("repositoryPath").GetString()
            ?? throw new InvalidOperationException($"Package '{packageKey}' tarball missing repositoryPath.");
        string sha256 = tarballElement.GetProperty("sha256").GetString()
            ?? throw new InvalidOperationException($"Package '{packageKey}' tarball missing sha256 hash.");
        long size = tarballElement.GetProperty("size").GetInt64();

        return new FrameworkPackageTarballMetadata(fileName, repositoryPath, sha256, size);
    }

    private static void VerifyTemplateDependencies(
        string repositoryRoot,
        IDictionary<string, FrameworkPackageTarballMetadata> tarballs,
        ICollection<string> failures)
    {
        if (tarballs.Count == 0)
        {
            return;
        }

        string templatePackageJsonPath = Path.Combine(repositoryRoot, "Engine", "Resources", "package.json");
        if (!File.Exists(templatePackageJsonPath))
        {
            failures.Add($"Template package.json not found at {templatePackageJsonPath}.");
            return;
        }

        using JsonDocument templateDocument = JsonDocument.Parse(File.ReadAllText(templatePackageJsonPath));
        if (!templateDocument.RootElement.TryGetProperty("dependencies", out JsonElement dependenciesElement) || dependenciesElement.ValueKind != JsonValueKind.Object)
        {
            failures.Add("Template package.json missing dependencies section.");
            return;
        }

        foreach (KeyValuePair<string, FrameworkPackageTarballMetadata> entry in tarballs)
        {
            string dependencyName = entry.Key;
            string expectedSpecifier = $"file:./.webstir/{entry.Value.FileName}";

            if (!dependenciesElement.TryGetProperty(dependencyName, out JsonElement dependencyValue))
            {
                failures.Add($"Template package.json missing dependency '{dependencyName}'.");
                continue;
            }

            string actualSpecifier = dependencyValue.GetString() ?? string.Empty;
            if (!string.Equals(actualSpecifier, expectedSpecifier, StringComparison.Ordinal))
            {
                failures.Add($"Template dependency for {dependencyName} expected '{expectedSpecifier}' but found '{actualSpecifier}'.");
            }
        }
    }

    private async Task<PackageDiffEntry> DiffPackageAsync(
        string repositoryRoot,
        JsonElement packagesElement,
        FrameworkPackageDescriptor descriptor)
    {
        if (!packagesElement.TryGetProperty(descriptor.PackageName, out JsonElement packageElement) || packageElement.ValueKind != JsonValueKind.Object)
        {
            return PackageDiffEntry.Missing(descriptor.PackageName, "Package metadata missing from framework-packages.json.");
        }

        FrameworkPackageTarballMetadata recordedTarball;
        try
        {
            recordedTarball = ReadTarballMetadata(descriptor.PackageName, packageElement);
        }
        catch (Exception ex)
        {
            return PackageDiffEntry.Missing(descriptor.PackageName, ex.Message);
        }

        string recordedVersion = packageElement.GetProperty("version").GetString() ?? string.Empty;

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

        string tempRoot = Path.Combine(Path.GetTempPath(), "webstir-packages-diff", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            await RunCommandAsync("npm", "ci --silent", packageDirectory, $"npm ci ({descriptor.PackageName})").ConfigureAwait(false);
            await RunCommandAsync("npm", "run build --silent", packageDirectory, $"npm run build ({descriptor.PackageName})").ConfigureAwait(false);

            DeleteMatchingFiles(packageDirectory, descriptor.TarballPattern);

            string createdTarballName = await RunPackAsync(packageDirectory, descriptor.PackageName).ConfigureAwait(false);
            string createdTarballPath = Path.Combine(packageDirectory, createdTarballName);

            string safeVersion = metadata.Version.Replace('.', '-');
            string targetTarballName = $"{descriptor.TarballPrefix}{safeVersion}.tgz";
            string tempTarballPath = Path.Combine(tempRoot, targetTarballName);

            if (!File.Exists(createdTarballPath))
            {
                return PackageDiffEntry.Missing(descriptor.PackageName, $"npm pack did not produce an expected tarball for {descriptor.PackageName}.");
            }

            if (File.Exists(tempTarballPath))
            {
                File.Delete(tempTarballPath);
            }

            File.Move(createdTarballPath, tempTarballPath);

            long actualSize = new FileInfo(tempTarballPath).Length;
            string actualSha = ComputeSha256(tempTarballPath);

            bool sizeMatches = actualSize == recordedTarball.Size;
            bool hashMatches = string.Equals(actualSha, recordedTarball.Sha256, StringComparison.OrdinalIgnoreCase);
            bool versionMatches = string.Equals(metadata.Version, recordedVersion, StringComparison.Ordinal);

            if (sizeMatches && hashMatches && versionMatches)
            {
                await CleanupPackageDirectoryAsync(packageDirectory, descriptor.CleanupDirectories, descriptor.TarballPattern).ConfigureAwait(false);
                return PackageDiffEntry.Unchanged(descriptor.PackageName, recordedTarball.Sha256, recordedTarball.Size);
            }

            string? message = versionMatches ? null : $"package.json version {metadata.Version} differs from recorded {recordedVersion}";
            await CleanupPackageDirectoryAsync(packageDirectory, descriptor.CleanupDirectories, descriptor.TarballPattern).ConfigureAwait(false);
            return PackageDiffEntry.Changed(descriptor.PackageName, recordedTarball.Sha256, recordedTarball.Size, actualSha, actualSize, message);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup failures
            }
        }
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

        await RunCommandAsync("npm", "ci --silent", packageDirectory, $"npm ci ({descriptor.PackageName})");
        await RunCommandAsync("npm", "run build --silent", packageDirectory, $"npm run build ({descriptor.PackageName})");

        DeleteMatchingFiles(packageDirectory, descriptor.TarballPattern);

        string createdTarballName = await RunPackAsync(packageDirectory, descriptor.PackageName);
        string safeVersion = metadata.Version.Replace('.', '-');
        string targetTarballName = $"{descriptor.TarballPrefix}{safeVersion}.tgz";
        string createdTarballPath = Path.Combine(packageDirectory, createdTarballName);
        string targetTarballPath = Path.Combine(packageDirectory, targetTarballName);
        if (!string.Equals(createdTarballName, targetTarballName, StringComparison.Ordinal))
        {
            if (File.Exists(targetTarballPath))
            {
                File.Delete(targetTarballPath);
            }

            File.Move(createdTarballPath, targetTarballPath);
        }
        else
        {
            targetTarballPath = createdTarballPath;
        }

        string repositoryRelativeTarballPath = GetRepositoryRelativePath(repositoryRoot, targetTarballPath);
        string tarballHash = ComputeSha256(targetTarballPath);
        long tarballSize = new FileInfo(targetTarballPath).Length;

        string resourcesDirectory = Path.Combine(repositoryRoot, "Framework", "Resources", "webstir");
        Directory.CreateDirectory(resourcesDirectory);
        DeleteMatchingFiles(resourcesDirectory, descriptor.TarballPattern);
        string resourceTarballPath = Path.Combine(resourcesDirectory, targetTarballName);
        File.Copy(targetTarballPath, resourceTarballPath, overwrite: true);

        FrameworkPackageTarballMetadata tarballMetadata = new(targetTarballName, repositoryRelativeTarballPath, tarballHash, tarballSize);

        string registrySpecifier = GetRegistrySpecifier(descriptor.RegistrySpecifierEnvironmentVariable)
            ?? descriptor.GetDefaultRegistrySpecifier(metadata.Version)
            ?? descriptor.GetPackageSpec(metadata.Version);

        bool published = false;
        if (publishPackages && descriptor.SupportsPublishing && !string.IsNullOrWhiteSpace(descriptor.PublishRegistryUrl))
        {
            string spec = descriptor.GetPackageSpec(metadata.Version);
            published = await PublishToRegistryAsync(spec, descriptor.PublishRegistryUrl!, targetTarballPath, descriptor.PublishAccess);
        }

        UpdatePackageCatalog(repositoryRoot, metadata.PackageName, metadata.Version, registrySpecifier, tarballMetadata);
        UpdateEngineResourcesPackageJson(Path.Combine(repositoryRoot, "Engine", "Resources", "package.json"), metadata.PackageName, tarballMetadata.WorkspaceSpecifier);

        await CleanupPackageDirectoryAsync(packageDirectory, descriptor.CleanupDirectories, descriptor.TarballPattern);

        return new PackageBuildResult(metadata.PackageName, metadata.Version, tarballMetadata, registrySpecifier, published);
    }

    private static PackageMetadata LoadPackageMetadata(string packageJsonPath, FrameworkPackageDescriptor descriptor)
    {
        using FileStream stream = File.OpenRead(packageJsonPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        string version = document.RootElement.GetProperty("version").GetString() ?? throw new InvalidOperationException($"Package version missing for {descriptor.PackageName}");
        string name = document.RootElement.GetProperty("name").GetString() ?? descriptor.PackageName;
        return new PackageMetadata(name, version);
    }

    private static async Task RunCommandAsync(string fileName, string arguments, string workingDirectory, string description)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                throw new InvalidOperationException($"Failed to start process for {description}.");
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                StringBuilder builder = new();
                builder.AppendLine(FormattableString.Invariant($"Command '{description}' failed with exit code {process.ExitCode}."));
                if (!string.IsNullOrWhiteSpace(error))
                {
                    builder.AppendLine(error.Trim());
                }
                if (!string.IsNullOrWhiteSpace(output))
                {
                    builder.AppendLine(output.Trim());
                }

                throw new InvalidOperationException(builder.ToString().Trim());
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Unable to execute '{fileName}'. Ensure it is installed and available on the PATH.", ex);
        }
    }

    private static async Task<string> RunPackAsync(string packageDirectory, string packageName)
    {
        ProcessStartInfo packInfo = new()
        {
            FileName = "npm",
            Arguments = "pack --silent",
            WorkingDirectory = packageDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using Process? process = Process.Start(packInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Failed to start npm pack for {packageName}.");
        }

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            string details = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException($"npm pack failed for {packageName}: {details.Trim()}");
        }

        string? tarballName = output.Trim().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(tarballName))
        {
            throw new InvalidOperationException($"npm pack did not return a tarball name for {packageName}.");
        }

        return tarballName;
    }

    private static void DeleteMatchingFiles(string directory, string searchPattern)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly))
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // best effort cleanup
            }
            catch (UnauthorizedAccessException)
            {
                // best effort cleanup
            }
        }
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

    private async Task<bool> PublishToRegistryAsync(string spec, string registryUrl, string tarballPath, string publishAccess)
    {
        if (string.IsNullOrWhiteSpace(registryUrl))
        {
            return false;
        }

        string directory = Path.GetDirectoryName(tarballPath)!;

        if (registryUrl.Contains("npm.pkg.github.com", StringComparison.OrdinalIgnoreCase))
        {
            string token = Environment.GetEnvironmentVariable("GH_PACKAGES_TOKEN") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("[packages] GH_PACKAGES_TOKEN is not set; skipping publish of {Spec}.", spec);
                return false;
            }
        }

        if (await PackageExistsAsync(spec, registryUrl, directory))
        {
            _logger.LogInformation("[packages] {Spec} already exists in {Registry}.", spec, registryUrl);
            return false;
        }

        string fileName = Path.GetFileName(tarballPath);

        ProcessStartInfo startInfo = new()
        {
            FileName = "npm",
            Arguments = $"publish \"{fileName}\" --registry \"{registryUrl}\" --access {publishAccess}",
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Failed to start npm publish for {spec}.");
        }

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            if (!string.IsNullOrWhiteSpace(error) && error.IndexOf("EPUBLISHCONFLICT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _logger.LogInformation("[packages] {Spec} already exists in {Registry}.", spec, registryUrl);
                return false;
            }

            StringBuilder builder = new();
            builder.AppendLine(FormattableString.Invariant($"npm publish failed for {spec} (exit {process.ExitCode})."));
            if (!string.IsNullOrWhiteSpace(error))
            {
                builder.AppendLine(error.Trim());
            }
            if (!string.IsNullOrWhiteSpace(output))
            {
                builder.AppendLine(output.Trim());
            }

            throw new InvalidOperationException(builder.ToString().Trim());
        }

        _logger.LogInformation("[packages] Published {Spec} to {Registry}.", spec, registryUrl);
        return true;
    }

    private static async Task<bool> PackageExistsAsync(string spec, string registryUrl, string workingDirectory)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "npm",
            Arguments = $"view {spec} version --registry \"{registryUrl}\"",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    private static void UpdatePackageCatalog(
        string repositoryRoot,
        string packageName,
        string version,
        string registrySpecifier,
        FrameworkPackageTarballMetadata tarball)
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
        JsonObject tarballNode = new()
        {
            ["fileName"] = tarball.FileName,
            ["repositoryPath"] = tarball.RepositoryPath,
            ["sha256"] = tarball.Sha256,
            ["size"] = tarball.Size
        };
        packageNode["tarball"] = tarballNode;

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

    private static void UpdateEngineResourcesPackageJson(string packageJsonPath, string packageName, string dependencySpecifier)
    {
        JsonObject root = File.Exists(packageJsonPath)
            ? JsonNode.Parse(File.ReadAllText(packageJsonPath)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        JsonObject dependencies = root["dependencies"] as JsonObject ?? new JsonObject();
        // Store only the plain version to avoid npm alias/link behavior when spec is "name@version"
        dependencies[packageName] = ExtractVersionFromSpecifier(dependencySpecifier) ?? dependencySpecifier;
        root["dependencies"] = dependencies;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(Path.GetDirectoryName(packageJsonPath)!);
        File.WriteAllText(packageJsonPath, root.ToJsonString(options) + Environment.NewLine);
    }

    private static string GetRepositoryRelativePath(string repositoryRoot, string absolutePath)
    {
        string relative = Path.GetRelativePath(repositoryRoot, absolutePath);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string ComputeSha256(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? ExtractVersionFromSpecifier(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return null;
        }

        // common forms:
        //   @scope/name@1.2.3
        //   npm:@scope/name@1.2.3
        //   name@1.2.3
        int lastAt = spec.LastIndexOf('@');
        if (lastAt >= 0 && lastAt + 1 < spec.Length)
        {
            string ver = spec[(lastAt + 1)..];
            // naive version check: starts with digit
            if (!string.IsNullOrWhiteSpace(ver) && char.IsDigit(ver[0]))
            {
                return ver;
            }
        }
        return null;
    }

    private static Task CleanupPackageDirectoryAsync(string packageDirectory, IReadOnlyCollection<string> directoriesToRemove, string tarballPattern)
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

        return Task.CompletedTask;
    }

    private readonly record struct PackageMetadata(string PackageName, string Version);

}

public readonly record struct PackageBuildResult(
    string PackageName,
    string Version,
    FrameworkPackageTarballMetadata Tarball,
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
    string ExpectedSha,
    long ExpectedSize,
    string? ActualSha,
    long? ActualSize)
{
    public static PackageDiffEntry Unchanged(string packageName, string expectedSha, long expectedSize) =>
        new(packageName, PackageDiffState.Unchanged, null, expectedSha, expectedSize, expectedSha, expectedSize);

    public static PackageDiffEntry Changed(string packageName, string expectedSha, long expectedSize, string actualSha, long actualSize, string? message) =>
        new(packageName, PackageDiffState.Changed, message, expectedSha, expectedSize, actualSha, actualSize);

    public static PackageDiffEntry Missing(string packageName, string message) =>
        new(packageName, PackageDiffState.Missing, message, string.Empty, 0, null, null);
}

public readonly record struct PackageDiffSummary(IReadOnlyList<PackageDiffEntry> Entries)
{
    public bool HasChanges
    {
        get
        {
            foreach (PackageDiffEntry entry in Entries)
            {
                if (entry.State != PackageDiffState.Unchanged)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
