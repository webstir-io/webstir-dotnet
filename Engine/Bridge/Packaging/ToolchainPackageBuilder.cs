using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Engine.Bridge.Packaging;

public sealed class ToolchainPackageBuilder
{
    private readonly ILogger<ToolchainPackageBuilder> _logger;

    public ToolchainPackageBuilder(ILogger<ToolchainPackageBuilder> logger)
    {
        _logger = logger;
    }

    internal static ToolchainManifestMetadata CreateManifestMetadata(string repositoryRoot) =>
        new ToolchainManifestMetadata(DateTimeOffset.UtcNow, TryGetGitCommit(repositoryRoot));

    private static string? TryGetGitCommit(string repositoryRoot)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadLine() ?? string.Empty;
            process.WaitForExit();
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    internal async Task<ToolchainPackageBuildResult> BuildFrontendAsync(string repositoryRoot, ToolchainManifestMetadata metadata, bool publish) =>
        await BuildAsync(repositoryRoot, ToolchainPackageOptions.Frontend, metadata, publish);

    internal async Task<ToolchainPackageBuildResult> BuildTestingAsync(string repositoryRoot, ToolchainManifestMetadata metadata, bool publish) =>
        await BuildAsync(repositoryRoot, ToolchainPackageOptions.Testing, metadata, publish);

    private async Task<ToolchainPackageBuildResult> BuildAsync(string repositoryRoot, ToolchainPackageOptions options, ToolchainManifestMetadata manifestMetadata, bool publishPackages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        string packageDirectory = Path.Combine(repositoryRoot, options.PackageRelativePath);
        if (!Directory.Exists(packageDirectory))
        {
            throw new DirectoryNotFoundException($"Package directory not found: {packageDirectory}");
        }

        string toolsDirectory = Path.Combine(repositoryRoot, "Engine", "Resources", "tools");
        string frameworkOutDirectory = Path.Combine(repositoryRoot, "framework", "out");
        string packageJsonPath = Path.Combine(packageDirectory, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            throw new FileNotFoundException($"package.json not found for {options.PackageName} at {packageJsonPath}");
        }

        PackageMetadata metadata = LoadPackageMetadata(packageJsonPath, options);

        await RunCommandAsync("npm", "ci --silent", packageDirectory, $"npm ci ({options.PackageName})");
        await RunCommandAsync("npm", "run build --silent", packageDirectory, $"npm run build ({options.PackageName})");

        DeleteMatchingFiles(packageDirectory, options.TarballPattern);

        string createdTarballName = await RunPackAsync(packageDirectory, options.PackageName);
        string safeVersion = metadata.Version.Replace('.', '-');
        string targetTarballName = $"{options.TarballPrefix}{safeVersion}.tgz";
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

        string repositoryPackageDirectory = Path.Combine(frameworkOutDirectory, options.RepositoryFolderName, metadata.Version);
        Directory.CreateDirectory(repositoryPackageDirectory);
        DeleteMatchingFiles(repositoryPackageDirectory, options.TarballPattern);
        string repositoryTarballPath = Path.Combine(repositoryPackageDirectory, targetTarballName);
        File.Copy(targetTarballPath, repositoryTarballPath, overwrite: true);

        Directory.CreateDirectory(toolsDirectory);
        DeleteMatchingFiles(toolsDirectory, options.TarballPattern);
        string toolsTarballPath = Path.Combine(toolsDirectory, targetTarballName);
        File.Copy(targetTarballPath, toolsTarballPath, overwrite: true);

        string hash = await ComputeSha256Async(repositoryTarballPath);

        string? registrySpecifier = GetRegistrySpecifier(options.RegistrySpecifierEnvironmentVariable)
            ?? options.GetDefaultRegistrySpecifier(metadata.Version);

        bool published = false;
        if (publishPackages && options.SupportsPublishing && !string.IsNullOrWhiteSpace(options.PublishRegistryUrl))
        {
            registrySpecifier ??= options.GetPackageSpec(metadata.Version);
            string spec = options.GetPackageSpec(metadata.Version);
            published = await PublishToRegistryAsync(spec, options.PublishRegistryUrl!, repositoryTarballPath, options.PublishAccess);
        }

        string manifestPath = Path.Combine(frameworkOutDirectory, "manifest.json");
        ToolchainManifestWriter.Update(manifestPath, new ToolchainManifestEntry(
            metadata.PackageName,
            metadata.Version,
            targetTarballName,
            $"file:./.tools/{targetTarballName}",
            hash,
            GetRepositoryPath(manifestPath, repositoryTarballPath),
            registrySpecifier), manifestMetadata);

        string toolsManifestPath = Path.Combine(toolsDirectory, options.ToolsManifestFileName);
        WriteToolsManifest(toolsManifestPath, metadata.PackageName, metadata.Version, targetTarballName, hash, registrySpecifier);

        string engineResourcesPackageJson = Path.Combine(repositoryRoot, "Engine", "Resources", "package.json");
        UpdateEngineResourcesPackageJson(engineResourcesPackageJson, metadata.PackageName, targetTarballName);

        await CleanupPackageDirectoryAsync(packageDirectory, options.CleanupDirectories, options.TarballPattern);

        return new ToolchainPackageBuildResult(metadata.PackageName, metadata.Version, targetTarballName, hash, registrySpecifier, published);
    }

    private static PackageMetadata LoadPackageMetadata(string packageJsonPath, ToolchainPackageOptions options)
    {
        using FileStream stream = File.OpenRead(packageJsonPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        string version = document.RootElement.GetProperty("version").GetString() ?? throw new InvalidOperationException($"Package version missing for {options.PackageName}");
        string name = document.RootElement.GetProperty("name").GetString() ?? options.PackageName;
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
                // best effort
            }
            catch (UnauthorizedAccessException)
            {
                // best effort
            }
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        await using FileStream stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetRepositoryPath(string manifestPath, string repositoryTarballPath)
    {
        string manifestDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
        string relative = Path.GetRelativePath(manifestDirectory, repositoryTarballPath);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
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
                _logger.LogWarning("[toolchain] GH_PACKAGES_TOKEN is not set; skipping publish of {Spec}.", spec);
                return false;
            }
        }

        if (await PackageExistsAsync(spec, registryUrl, directory))
        {
            _logger.LogInformation("[toolchain] {Spec} already exists in {Registry}.", spec, registryUrl);
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
                _logger.LogInformation("[toolchain] {Spec} already exists in {Registry}.", spec, registryUrl);
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

        _logger.LogInformation("[toolchain] Published {Spec} to {Registry}.", spec, registryUrl);
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

    private static void WriteToolsManifest(string manifestPath, string packageName, string version, string tarballName, string hash, string? registrySpecifier)
    {
        JsonObject manifest = new()
        {
            ["name"] = packageName,
            ["version"] = version,
            ["fileName"] = tarballName,
            ["dependency"] = $"file:./.tools/{tarballName}",
            ["hash"] = hash
        };

        if (!string.IsNullOrWhiteSpace(registrySpecifier))
        {
            manifest["registrySpecifier"] = registrySpecifier;
        }

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, manifest.ToJsonString(options) + Environment.NewLine);
    }

    private static void UpdateEngineResourcesPackageJson(string packageJsonPath, string packageName, string tarballName)
    {
        JsonObject root;
        if (File.Exists(packageJsonPath))
        {
            root = JsonNode.Parse(File.ReadAllText(packageJsonPath)) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        JsonObject dependencies = root["dependencies"] as JsonObject ?? new JsonObject();
        dependencies[packageName] = $"file:./.tools/{tarballName}";
        root["dependencies"] = dependencies;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(Path.GetDirectoryName(packageJsonPath)!);
        File.WriteAllText(packageJsonPath, root.ToJsonString(options) + Environment.NewLine);
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

    private sealed record ToolchainPackageOptions(
        string PackageName,
        string PackageRelativePath,
        string TarballPrefix,
        string TarballPattern,
        string RepositoryFolderName,
        string ToolsManifestFileName,
        string? RegistrySpecifierEnvironmentVariable,
        IReadOnlyCollection<string> CleanupDirectories,
        string? DefaultRegistrySpecifierPattern,
        string? PublishRegistryUrl,
        string PublishAccess)
    {
        internal static ToolchainPackageOptions Frontend
        {
            get;
        } = new(
            "@electric-coding-llc/webstir-frontend",
            Path.Combine("framework", "frontend"),
            "webstir-frontend-",
            "webstir-frontend-*.tgz",
            "frontend",
            "frontend-package.json",
            "WEBSTIR_FRONTEND_REGISTRY_SPEC",
            new[] { "node_modules" },
            "@electric-coding-llc/webstir-frontend@{version}",
            "https://npm.pkg.github.com",
            "restricted");

        internal string? GetDefaultRegistrySpecifier(string version) => string.IsNullOrWhiteSpace(DefaultRegistrySpecifierPattern) ? null : DefaultRegistrySpecifierPattern.Replace("{version}", version, StringComparison.Ordinal);

        internal bool SupportsPublishing => !string.IsNullOrWhiteSpace(PublishRegistryUrl);

        internal string GetPackageSpec(string version) => $"{PackageName}@{version}";

        internal static ToolchainPackageOptions Testing
        {
            get;
        } = new(
            "@electric-coding-llc/webstir-test",
            Path.Combine("framework", "testing"),
            "webstir-test-",
            "webstir-test-*.tgz",
            "testing",
            "testing-package.json",
            "WEBSTIR_TEST_REGISTRY_SPEC",
            new[] { "node_modules", "dist" },
            "@electric-coding-llc/webstir-test@{version}",
            "https://npm.pkg.github.com",
            "restricted");
    }
}

internal readonly record struct ToolchainPackageBuildResult(
    string PackageName,
    string Version,
    string TarballName,
    string Hash,
    string? RegistrySpecifier,
    bool Published);

internal readonly record struct ToolchainManifestEntry(
    string Name,
    string Version,
    string FileName,
    string Dependency,
    string Hash,
    string RepositoryPath,
    string? RegistrySpecifier);

internal readonly record struct ToolchainManifestMetadata(DateTimeOffset GeneratedAtUtc, string? Commit)
{
    internal JsonObject ToJson()
    {
        JsonObject obj = new()
        {
            ["generatedAtUtc"] = GeneratedAtUtc.ToString("O")
        };
        if (!string.IsNullOrWhiteSpace(Commit))
        {
            obj["commit"] = Commit;
        }
        return obj;
    }
}

internal static class ToolchainManifestWriter
{
    internal static void Update(string manifestPath, ToolchainManifestEntry entry, ToolchainManifestMetadata metadata)
    {
        JsonObject root;
        if (File.Exists(manifestPath))
        {
            root = JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject
            {
                ["schemaVersion"] = 1
            };
        }

        int schemaVersion = root["schemaVersion"]?.GetValue<int>() ?? 1;
        JsonObject packages = root["packages"] as JsonObject ?? new JsonObject();

        packages[entry.Name] = CreatePackageNode(entry);

        JsonObject sortedPackages = new();
        foreach (KeyValuePair<string, JsonNode?> item in packages.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            sortedPackages[item.Key] = item.Value?.DeepClone();
        }

        JsonObject output = new()
        {
            ["schemaVersion"] = schemaVersion,
            ["packages"] = sortedPackages
        };

        output["metadata"] = metadata.ToJson();

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        string serialized = output.ToJsonString(options) + Environment.NewLine;
        if (File.Exists(manifestPath) && string.Equals(File.ReadAllText(manifestPath), serialized, StringComparison.Ordinal))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, serialized);
    }

    private static JsonObject CreatePackageNode(ToolchainManifestEntry entry)
    {
        JsonObject packageNode = new()
        {
            ["name"] = entry.Name,
            ["version"] = entry.Version,
            ["fileName"] = entry.FileName,
            ["dependency"] = entry.Dependency,
            ["hash"] = entry.Hash,
            ["repositoryPath"] = entry.RepositoryPath
        };

        if (!string.IsNullOrWhiteSpace(entry.RegistrySpecifier))
        {
            packageNode["registrySpecifier"] = entry.RegistrySpecifier;
        }

        return packageNode;
    }
}
