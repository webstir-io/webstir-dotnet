using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Engine.Bridge.Packaging;
using Engine.Interfaces;
using Microsoft.Extensions.Logging;

namespace Engine.Workflows;

public sealed class ToolchainWorkflow(
    ILogger<ToolchainWorkflow> logger,
    ToolchainPackageBuilder packageBuilder) : IWorkflow
{
    private const string SyncCommand = "sync";
    private const string VerifyCommand = "verify";
    private const string PublishCommand = "publish";
    private readonly ILogger<ToolchainWorkflow> _logger = logger;
    private readonly ToolchainPackageBuilder _packageBuilder = packageBuilder;

    public string WorkflowName => Commands.Toolchain;

    public async Task ExecuteAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string repositoryRoot = Directory.GetCurrentDirectory();
        ParsedArguments parsed = ParseArguments(args);

        if (parsed.Mode == VerifyCommand)
        {
            await VerifyAsync(repositoryRoot);
            return;
        }

        await RunSyncAsync(repositoryRoot, parsed, parsed.PublishPackages);

        if (parsed.RunVerifyAfterSync)
        {
            await VerifyAsync(repositoryRoot);
        }
    }

    private async Task RunSyncAsync(string repositoryRoot, ParsedArguments parsed, bool publishPackages)
    {
        _logger.LogInformation("[toolchain] Synchronizing framework packages...");
        if (publishPackages)
        {
            _logger.LogInformation("[toolchain] Publish mode enabled; packages will be pushed to the registry if missing.");
        }

        ToolchainManifestMetadata manifestMetadata = ToolchainPackageBuilder.CreateManifestMetadata(repositoryRoot);

        if (parsed.IncludeFrontend)
        {
            ToolchainPackageBuildResult result = await _packageBuilder.BuildFrontendAsync(repositoryRoot, manifestMetadata, publishPackages);
            _logger.LogInformation(
                "[toolchain] Rebuilt {Package} {Version} (hash {Hash}).",
                result.PackageName,
                result.Version,
                result.Hash);

            if (publishPackages && result.Published)
            {
                _logger.LogInformation("[toolchain] Published {Package}@{Version} to registry.", result.PackageName, result.Version);
            }

            if (publishPackages && result.Published)
            {
                _logger.LogInformation("[toolchain] Published {Package}@{Version} to registry.", result.PackageName, result.Version);
            }
        }

        if (parsed.IncludeTesting)
        {
            ToolchainPackageBuildResult result = await _packageBuilder.BuildTestingAsync(repositoryRoot, manifestMetadata, publishPackages);
            _logger.LogInformation(
                "[toolchain] Rebuilt {Package} {Version} (hash {Hash}).",
                result.PackageName,
                result.Version,
                result.Hash);
        }

        _logger.LogInformation("[toolchain] Toolchain sync complete.");
    }

    private async Task VerifyAsync(string repositoryRoot)
    {
        _logger.LogInformation("[toolchain] Verifying committed toolchain artifacts...");

        ProcessStartInfo startInfo = new()
        {
            FileName = "git",
            Arguments = "status --porcelain -- Engine/Resources/tools framework/out",
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
            string trimmed = output.Trim();
            if (string.Equals(trimmed, "M framework/out/manifest.json", StringComparison.Ordinal) && ManifestMatchesHead(repositoryRoot))
            {
                _logger.LogInformation("[toolchain] Manifest metadata reflects current HEAD (uncommitted); remember to commit the updated manifest.");
            }
            else
            {
                Console.Write(output);
                throw new InvalidOperationException("Toolchain artifacts are out of sync. Run 'webstir toolchain sync' and commit the changes.");
            }
        }

        ValidateManifestMetadata(repositoryRoot);
        _logger.LogInformation("[toolchain] Toolchain artifacts are in sync.");
    }

    private bool ManifestMatchesHead(string repositoryRoot)
    {
        string manifestPath = Path.Combine(repositoryRoot, "framework", "out", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!document.RootElement.TryGetProperty("metadata", out JsonElement metadata) || metadata.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        string? manifestCommit = metadata.TryGetProperty("commit", out JsonElement commitElement) ? commitElement.GetString() : null;
        string? currentCommit = ToolchainPackageBuilder.CreateManifestMetadata(repositoryRoot).Commit;
        return !string.IsNullOrWhiteSpace(manifestCommit) && string.Equals(manifestCommit, currentCommit, StringComparison.Ordinal);
    }

    private void ValidateManifestMetadata(string repositoryRoot)
    {
        string manifestPath = Path.Combine(repositoryRoot, "framework", "out", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException("Toolchain manifest not found. Run 'webstir toolchain sync'.");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("metadata", out JsonElement metadataElement) || metadataElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Toolchain manifest missing metadata. Run 'webstir toolchain sync'.");
        }

        if (!metadataElement.TryGetProperty("generatedAtUtc", out JsonElement generatedAtElement) || generatedAtElement.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(generatedAtElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
        {
            throw new InvalidOperationException("Toolchain manifest metadata missing generatedAtUtc timestamp. Run 'webstir toolchain sync'.");
        }

        if (!metadataElement.TryGetProperty("commit", out JsonElement commitElement) || commitElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(commitElement.GetString()))
        {
            throw new InvalidOperationException("Toolchain manifest metadata missing commit hash. Run 'webstir toolchain sync'.");
        }

        string? manifestCommit = commitElement.GetString();
        string? currentCommit = ToolchainPackageBuilder.CreateManifestMetadata(repositoryRoot).Commit;
        if (!string.IsNullOrWhiteSpace(currentCommit) && !string.Equals(manifestCommit, currentCommit, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Toolchain manifest metadata commit does not match current HEAD. Run 'webstir toolchain sync'.");
        }
    }

    private static ParsedArguments ParseArguments(string[] args)
    {
        string mode = SyncCommand;
        bool includeFrontend = true;
        bool includeTesting = true;
        bool verifyAfterSync = false;
        bool publishPackages = false;

        int index = 0;
        if (args.Length > 0 && string.Equals(args[0], Commands.Toolchain, StringComparison.OrdinalIgnoreCase))
        {
            index = 1;
        }

    if (index < args.Length && !IsOption(args[index]))
        {
            mode = args[index].ToLowerInvariant();
            index++;
        }

        if (string.Equals(mode, PublishCommand, StringComparison.OrdinalIgnoreCase))
        {
            publishPackages = true;
            verifyAfterSync = true;
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
                    verifyAfterSync = true;
                    break;
                case "--publish":
                    publishPackages = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown toolchain option '{current}'.");
            }
        }

        if (mode is not SyncCommand and not VerifyCommand and not PublishCommand)
        {
            throw new InvalidOperationException($"Unknown toolchain command '{mode}'. Use '{SyncCommand}', '{PublishCommand}', or '{VerifyCommand}'.");
        }

        if (publishPackages)
        {
            verifyAfterSync = true;
        }

        if ((mode == SyncCommand || mode == PublishCommand) && !includeFrontend && !includeTesting)
        {
            includeFrontend = true;
            includeTesting = true;
        }

        return new ParsedArguments(mode, includeFrontend, includeTesting, verifyAfterSync, publishPackages);
    }

    private static bool IsOption(string value) => value.StartsWith("-", StringComparison.Ordinal);

    private readonly record struct ParsedArguments(
        string Mode,
        bool IncludeFrontend,
        bool IncludeTesting,
        bool RunVerifyAfterSync,
        bool PublishPackages);
}
