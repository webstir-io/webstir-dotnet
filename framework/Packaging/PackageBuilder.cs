using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Framework.Packaging;

public sealed class PackageBuilder
{
    private readonly ILogger<PackageBuilder> _logger;

    public PackageBuilder(ILogger<PackageBuilder> logger)
    {
        _logger = logger;
    }

    public async Task<PackageBuildResult> BuildFrontendAsync(string repositoryRoot, bool publish) =>
        await BuildAsync(repositoryRoot, PackageBuildOptions.Frontend, publish);

    public async Task<PackageBuildResult> BuildTestingAsync(string repositoryRoot, bool publish) =>
        await BuildAsync(repositoryRoot, PackageBuildOptions.Testing, publish);

    private async Task<PackageBuildResult> BuildAsync(string repositoryRoot, PackageBuildOptions options, bool publishPackages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        string packageDirectory = Path.Combine(repositoryRoot, options.PackageRelativePath);
        if (!Directory.Exists(packageDirectory))
        {
            throw new DirectoryNotFoundException($"Package directory not found: {packageDirectory}");
        }

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

        string registrySpecifier = GetRegistrySpecifier(options.RegistrySpecifierEnvironmentVariable)
            ?? options.GetDefaultRegistrySpecifier(metadata.Version)
            ?? options.GetPackageSpec(metadata.Version);

        bool published = false;
        if (publishPackages && options.SupportsPublishing && !string.IsNullOrWhiteSpace(options.PublishRegistryUrl))
        {
            string spec = options.GetPackageSpec(metadata.Version);
            published = await PublishToRegistryAsync(spec, options.PublishRegistryUrl!, targetTarballPath, options.PublishAccess);
        }

        UpdatePackageCatalog(repositoryRoot, metadata.PackageName, metadata.Version, registrySpecifier);
        UpdateEngineResourcesPackageJson(Path.Combine(repositoryRoot, "Engine", "Resources", "package.json"), metadata.PackageName, registrySpecifier);

        await CleanupPackageDirectoryAsync(packageDirectory, options.CleanupDirectories, options.TarballPattern);

        return new PackageBuildResult(metadata.PackageName, metadata.Version, targetTarballPath, registrySpecifier, published);
    }

    private static PackageMetadata LoadPackageMetadata(string packageJsonPath, PackageBuildOptions options)
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

    private static void UpdatePackageCatalog(string repositoryRoot, string packageName, string version, string registrySpecifier)
    {
        string catalogPath = Path.Combine(repositoryRoot, "framework", "Packaging", "framework-packages.json");
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
        dependencies[packageName] = dependencySpecifier;
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

    private sealed record PackageBuildOptions(
        string PackageName,
        string PackageRelativePath,
        string TarballPrefix,
        string TarballPattern,
        string? RegistrySpecifierEnvironmentVariable,
        IReadOnlyCollection<string> CleanupDirectories,
        string? DefaultRegistrySpecifierPattern,
        string? PublishRegistryUrl,
        string PublishAccess)
    {
        internal static PackageBuildOptions Frontend { get; } = new(
            "@electric-coding-llc/webstir-frontend",
            Path.Combine("framework", "frontend"),
            "webstir-frontend-",
            "webstir-frontend-*.tgz",
            "WEBSTIR_FRONTEND_REGISTRY_SPEC",
            new[] { "node_modules" },
            "@electric-coding-llc/webstir-frontend@{version}",
            "https://npm.pkg.github.com",
            "restricted");

        internal static PackageBuildOptions Testing { get; } = new(
            "@electric-coding-llc/webstir-test",
            Path.Combine("framework", "testing"),
            "webstir-test-",
            "webstir-test-*.tgz",
            "WEBSTIR_TEST_REGISTRY_SPEC",
            new[] { "node_modules", "dist" },
            "@electric-coding-llc/webstir-test@{version}",
            "https://npm.pkg.github.com",
            "restricted");

        internal string? GetDefaultRegistrySpecifier(string version) =>
            string.IsNullOrWhiteSpace(DefaultRegistrySpecifierPattern)
                ? null
                : DefaultRegistrySpecifierPattern.Replace("{version}", version, StringComparison.Ordinal);

        internal bool SupportsPublishing => !string.IsNullOrWhiteSpace(PublishRegistryUrl);

        internal string GetPackageSpec(string version) => $"{PackageName}@{version}";
    }
}

public readonly record struct PackageBuildResult(
    string PackageName,
    string Version,
    string TarballPath,
    string RegistrySpecifier,
    bool Published);
