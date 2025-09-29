using System;
using System.Diagnostics;
using System.IO;
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

        await RunSyncAsync(repositoryRoot, parsed);

        if (parsed.RunVerifyAfterSync)
        {
            await VerifyAsync(repositoryRoot);
        }
    }

    private async Task RunSyncAsync(string repositoryRoot, ParsedArguments parsed)
    {
        _logger.LogInformation("[toolchain] Synchronizing framework packages...");

        if (parsed.IncludeFrontend)
        {
            ToolchainPackageBuildResult result = await _packageBuilder.BuildFrontendAsync(repositoryRoot);
            _logger.LogInformation(
                "[toolchain] Rebuilt {Package} {Version} (hash {Hash}).",
                result.PackageName,
                result.Version,
                result.Hash);
        }

        if (parsed.IncludeTesting)
        {
            ToolchainPackageBuildResult result = await _packageBuilder.BuildTestingAsync(repositoryRoot);
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

        if (string.IsNullOrWhiteSpace(output))
        {
            _logger.LogInformation("[toolchain] Toolchain artifacts are in sync.");
            return;
        }

        Console.Write(output);
        throw new InvalidOperationException("Toolchain artifacts are out of sync. Run 'webstir toolchain sync' and commit the changes.");
    }

    private static ParsedArguments ParseArguments(string[] args)
    {
        string mode = SyncCommand;
        bool includeFrontend = true;
        bool includeTesting = true;
        bool verifyAfterSync = false;

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
                default:
                    throw new InvalidOperationException($"Unknown toolchain option '{current}'.");
            }
        }

        if (mode is not SyncCommand and not VerifyCommand)
        {
            throw new InvalidOperationException($"Unknown toolchain command '{mode}'. Use '{SyncCommand}' or '{VerifyCommand}'.");
        }

        if (mode == SyncCommand && !includeFrontend && !includeTesting)
        {
            includeFrontend = true;
            includeTesting = true;
        }

        return new ParsedArguments(mode, includeFrontend, includeTesting, verifyAfterSync);
    }

    private static bool IsOption(string value) => value.StartsWith("-", StringComparison.Ordinal);

    private readonly record struct ParsedArguments(
        string Mode,
        bool IncludeFrontend,
        bool IncludeTesting,
        bool RunVerifyAfterSync);
}
