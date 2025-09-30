namespace Framework.Commands;

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Framework.Packaging;
using Microsoft.Extensions.Logging;

internal sealed class PackageConsoleCommand
{
    private const string SyncCommand = "sync";
    private const string PublishCommand = "publish";
    private const string VerifyCommand = "verify";

    private readonly PackageBuilder _packageBuilder;
    private readonly ILogger<PackageConsoleCommand> _logger;

    public PackageConsoleCommand(PackageBuilder packageBuilder, ILogger<PackageConsoleCommand> logger)
    {
        _packageBuilder = packageBuilder;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            ParsedArguments parsed = ParseArguments(args);
            string repositoryRoot = Directory.GetCurrentDirectory();

            if (parsed.Mode == VerifyCommand)
            {
                await VerifyAsync(repositoryRoot);
                return 0;
            }

            await RunSyncAsync(repositoryRoot, parsed, parsed.PublishPackages);

            if (parsed.RunVerifyAfterSync)
            {
                await VerifyAsync(repositoryRoot);
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "framework packages command failed.");
            return 1;
        }
    }

    private async Task RunSyncAsync(string repositoryRoot, ParsedArguments parsed, bool publishPackages)
    {
        _logger.LogInformation("[packages] Synchronizing framework packages...");
        if (publishPackages)
        {
            _logger.LogInformation("[packages] Publish mode enabled; packages will be pushed to the registry if missing.");
        }

        PackageManifestMetadata manifestMetadata = PackageBuilder.CreateManifestMetadata();

        if (parsed.IncludeFrontend)
        {
            PackageBuildResult result = await _packageBuilder.BuildFrontendAsync(repositoryRoot, manifestMetadata, publishPackages);
            _logger.LogInformation(
                "[packages] Rebuilt {Package} {Version} (hash {Hash}).",
                result.PackageName,
                result.Version,
                result.Hash);

            if (publishPackages && result.Published)
            {
                _logger.LogInformation("[packages] Published {Package}@{Version} to registry.", result.PackageName, result.Version);
            }
        }

        if (parsed.IncludeTesting)
        {
            PackageBuildResult result = await _packageBuilder.BuildTestingAsync(repositoryRoot, manifestMetadata, publishPackages);
            _logger.LogInformation(
                "[packages] Rebuilt {Package} {Version} (hash {Hash}).",
                result.PackageName,
                result.Version,
                result.Hash);
        }

        _logger.LogInformation("[packages] Package sync complete.");
    }

    private async Task VerifyAsync(string repositoryRoot)
    {
        _logger.LogInformation("[packages] Verifying committed package artifacts...");

        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            Arguments = "status --porcelain -- framework/Resources/tools framework/out",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using Process? process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start 'git status' for verification.");
        }

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            string message = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException($"git status failed during verification: {message.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            Console.Write(output);
            throw new InvalidOperationException("Package artifacts are out of sync. Run 'framework packages sync' and commit the changes.");
        }

        ValidateManifestArtifacts(repositoryRoot);
        _logger.LogInformation("[packages] Package artifacts are in sync.");
    }

    private void ValidateManifestArtifacts(string repositoryRoot)
    {
        string manifestPath = Path.Combine(repositoryRoot, "framework", "out", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException("Package manifest not found. Run 'framework packages sync'.");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("packages", out JsonElement packagesElement) || packagesElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Package manifest missing packages. Run 'framework packages sync'.");
        }

        foreach (JsonProperty package in packagesElement.EnumerateObject())
        {
            JsonElement packageNode = package.Value;
            string name = packageNode.GetProperty("name").GetString() ?? package.Name;
            string hash = packageNode.GetProperty("hash").GetString() ?? string.Empty;
            string fileName = packageNode.GetProperty("fileName").GetString() ?? string.Empty;
            string dependency = packageNode.GetProperty("dependency").GetString() ?? string.Empty;
            string repositoryPath = packageNode.GetProperty("repositoryPath").GetString() ?? string.Empty;

            EnsureHashMatches(Path.Combine(repositoryRoot, "framework", "Resources", "tools", fileName), hash, name);
            EnsureHashMatches(Path.Combine(repositoryRoot, repositoryPath.Replace('/', Path.DirectorySeparatorChar)), hash, name);

            if (!dependency.EndsWith(fileName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Manifest dependency for {name} does not match tarball name. Run 'framework packages sync'.");
            }
        }
    }

    private static void EnsureHashMatches(string filePath, string expectedHash, string packageName)
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"Expected tarball missing for {packageName}: {filePath}. Run 'framework packages sync'.");
        }

        using FileStream stream = File.OpenRead(filePath);
        string actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Tarball hash mismatch for {packageName} (expected {expectedHash}, found {actualHash}). Run 'framework packages sync'.");
        }
    }

    private static ParsedArguments ParseArguments(string[] args)
    {
        string mode = SyncCommand;
        bool includeFrontend = true;
        bool includeTesting = true;
        bool runVerifyAfterSync = false;
        bool publishPackages = false;

        int index = 0;
        if (args.Length > 0 && !IsOption(args[0]))
        {
            mode = args[0].ToLowerInvariant();
            index = 1;
        }

        for (int i = index; i < args.Length; i++)
        {
            string current = args[i];
            switch (current)
            {
                case "--frontend":
                case "-f":
                    includeFrontend = true;
                    includeTesting = false;
                    break;
                case "--test":
                case "-t":
                    includeFrontend = false;
                    includeTesting = true;
                    break;
                case "--both":
                    includeFrontend = true;
                    includeTesting = true;
                    break;
                case "--verify":
                    runVerifyAfterSync = true;
                    break;
                case "--publish":
                    publishPackages = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown packages option '{current}'.");
            }
        }

        if (mode == PublishCommand)
        {
            publishPackages = true;
            runVerifyAfterSync = true;
            mode = SyncCommand;
        }

        if (mode is not SyncCommand and not VerifyCommand)
        {
            throw new InvalidOperationException($"Unknown packages command '{mode}'. Use '{SyncCommand}' or '{VerifyCommand}'.");
        }

        if (mode == SyncCommand && !includeFrontend && !includeTesting)
        {
            includeFrontend = true;
            includeTesting = true;
        }

        return new ParsedArguments(mode, includeFrontend, includeTesting, runVerifyAfterSync, publishPackages);
    }

    private static bool IsOption(string value) => value.StartsWith("-", StringComparison.Ordinal);

    private readonly record struct ParsedArguments(
        string Mode,
        bool IncludeFrontend,
        bool IncludeTesting,
        bool RunVerifyAfterSync,
        bool PublishPackages);
}
