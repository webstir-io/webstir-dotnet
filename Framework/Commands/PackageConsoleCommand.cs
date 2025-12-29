using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Framework.Services;
using Framework.Utilities;
using Microsoft.Extensions.Logging;

namespace Framework.Commands;

internal sealed class PackageConsoleCommand
{
    private static readonly string[] HelpTokens = ["-h", "--help", "help"];

    private readonly ILogger<PackageConsoleCommand> _logger;
    private readonly IReadOnlyDictionary<string, IPackagesSubcommand> _handlers;

    public PackageConsoleCommand(
        IEnumerable<IPackagesSubcommand> subcommands,
        ILogger<PackageConsoleCommand> logger)
    {
        ArgumentNullException.ThrowIfNull(subcommands);

        _logger = logger;
        _handlers = BuildCommandIndex(subcommands);
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            PackagesParseResult result = ParseArguments(args);
            if (result.ShowHelp)
            {
                ShowUsage();
                return 0;
            }

            if (!_handlers.TryGetValue(result.Command, out IPackagesSubcommand? handler))
            {
                _logger.LogError("[packages] Unknown command '{Command}'.", result.Command);
                ShowUsage();
                return 1;
            }

            PackagesCommandContext context = new(
                result.Command,
                result.RepositoryRoot,
                result.Selection,
                result.DryRun,
                result.Bump,
                result.BumpExplicit,
                result.ExplicitVersion,
                result.PrintVersion,
                result.Interactive,
                result.SinceReference,
                result.AdditionalArguments);

            return await handler.ExecuteAsync(context, CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError("[packages] {Message}", ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "framework packages command failed.");
            return 1;
        }
    }

    private static IReadOnlyDictionary<string, IPackagesSubcommand> BuildCommandIndex(IEnumerable<IPackagesSubcommand> handlers)
    {
        Dictionary<string, IPackagesSubcommand> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (IPackagesSubcommand handler in handlers)
        {
            map.TryAdd(handler.Name, handler);
            foreach (string alias in handler.Aliases)
            {
                map.TryAdd(alias, handler);
            }
        }

        return map;
    }

    private PackagesParseResult ParseArguments(string[] args)
    {
        string repositoryRoot = Directory.GetCurrentDirectory();
        if (ContainsHelp(args))
        {
            string commandName = args.Length > 0 && !IsOption(args[0])
                ? NormalizeCommand(args[0])
                : "sync";

            return new PackagesParseResult(
                commandName,
                ShowHelp: true,
                repositoryRoot,
                PackageSelection.ChangedPackages,
                DryRun: false,
                Bump: SemanticVersionBump.Patch,
                BumpExplicit: false,
                ExplicitVersion: null,
                PrintVersion: false,
                Interactive: false,
                SinceReference: null,
                AdditionalArguments: Array.Empty<string>());
        }

        int index = 0;
        string command = "sync";

        if (args.Length > 0 && !IsOption(args[0]))
        {
            command = NormalizeCommand(args[0]);
            index = 1;
        }

        HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
        bool allPackages = false;
        bool changedOnly = false;
        bool dryRun = false;
        bool interactive = false;
        bool bumpExplicit = false;
        bool printVersion = false;
        string? sinceReference = null;
        SemanticVersionBump bump = SemanticVersionBump.Patch;
        SemanticVersion? explicitVersion = null;
        List<string> additionalArguments = new();

        for (int i = index; i < args.Length; i++)
        {
            string token = args[i];
            if (token == "--")
            {
                additionalArguments.AddRange(args[(i + 1)..]);
                break;
            }

            if (IsHelp(token))
            {
                return new PackagesParseResult(
                    command,
                    ShowHelp: true,
                    repositoryRoot,
                    PackageSelection.ChangedPackages,
                    dryRun,
                    bump,
                    bumpExplicit,
                    explicitVersion,
                    PrintVersion: false,
                    interactive,
                    sinceReference,
                    additionalArguments);
            }

            switch (token)
            {
                case "--package":
                case "-p":
                    string identifier = RequireValue(args, ref i, token);
                    foreach (string value in SplitIdentifiers(identifier))
                    {
                        identifiers.Add(value);
                    }

                    break;
                case "--frontend":
                    identifiers.Add("frontend");
                    break;
                case "--test":
                case "--testing":
                    identifiers.Add("testing");
                    break;
                case "--backend":
                    identifiers.Add("backend");
                    break;
                case "--both":
                case "--all":
                    allPackages = true;
                    break;
                case "--changed-only":
                    changedOnly = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--print-version":
                    printVersion = true;
                    break;
                case "--interactive":
                    interactive = true;
                    break;
                case "--set-version":
                    explicitVersion = ParseVersion(RequireValue(args, ref i, token));
                    break;
                case "--bump":
                case "-b":
                    bump = ParseBump(RequireValue(args, ref i, token));
                    bumpExplicit = true;
                    break;
                case "--since":
                    sinceReference = RequireValue(args, ref i, token);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown packages option '{token}'.");
            }
        }

        if (explicitVersion is not null && bumpExplicit)
        {
            throw new InvalidOperationException("--set-version cannot be combined with --bump.");
        }

        if (changedOnly && (allPackages || identifiers.Count > 0))
        {
            throw new InvalidOperationException("--changed-only cannot be combined with --all or --package.");
        }

        PackageSelection selection = DetermineSelection(allPackages, changedOnly, identifiers, sinceReference);

        if (printVersion && !command.Equals("bump", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("--print-version is only supported for the bump command.");
        }

        return new PackagesParseResult(
            command,
            ShowHelp: false,
            repositoryRoot,
            selection,
            dryRun,
            bump,
            bumpExplicit,
            explicitVersion,
            printVersion,
            interactive,
            sinceReference,
            additionalArguments);
    }

    private static PackageSelection DetermineSelection(bool allPackages, bool changedOnly, HashSet<string> identifiers, string? sinceReference)
    {
        if (identifiers.Count > 0)
        {
            string[] values = identifiers.ToArray();
            return PackageSelection.Explicit(values);
        }

        if (allPackages)
        {
            return PackageSelection.AllPackages;
        }

        if (changedOnly || !string.IsNullOrWhiteSpace(sinceReference))
        {
            return PackageSelection.ChangedPackages;
        }

        return PackageSelection.ChangedPackages;
    }

    private static bool ContainsHelp(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        return args.Any(IsHelp);
    }

    private static bool IsHelp(string value) =>
        HelpTokens.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static bool IsOption(string value) =>
        value.StartsWith("-", StringComparison.Ordinal);

    private static string NormalizeCommand(string value)
    {
        return value.ToLower(CultureInfo.InvariantCulture) switch
        {
            "sync" or "build" => "sync",
            "publish" => "publish",
            "release" => "release",
            "bump" => "bump",
            "verify" => "verify",
            "diff" => "diff",
            _ => value.ToLower(CultureInfo.InvariantCulture)
        };
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException($"Option '{option}' requires a value.");
        }

        index++;
        return args[index];
    }

    private static IEnumerable<string> SplitIdentifiers(string value)
    {
        return value.Split(
            new[] { ',', ';' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static SemanticVersion ParseVersion(string value)
    {
        try
        {
            return SemanticVersion.Parse(value);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Invalid semantic version '{value}'.", ex);
        }
    }

    private static SemanticVersionBump ParseBump(string value)
    {
        return value.ToLower(CultureInfo.InvariantCulture) switch
        {
            "major" => SemanticVersionBump.Major,
            "minor" => SemanticVersionBump.Minor,
            "patch" => SemanticVersionBump.Patch,
            _ => throw new InvalidOperationException($"Unsupported bump value '{value}'.")
        };
    }

    private static void ShowUsage()
    {
        Console.WriteLine("framework packages <bump|sync|release|publish|verify|diff> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  bump       Bump package versions using conventional commit heuristics or manual flags.");
        Console.WriteLine("  sync       Rebuild framework packages and refresh registry metadata.");
        Console.WriteLine("  release    Bump and rebuild packages without publishing.");
        Console.WriteLine("  publish    Bump, rebuild, and publish packages to the configured registry.");
        Console.WriteLine("  verify     Validate registry metadata and template dependencies.");
        Console.WriteLine("  diff       Report registry metadata differences without modifying files.");
        Console.WriteLine();
        Console.WriteLine("Shared Options:");
        Console.WriteLine("  --package <name>    Target a specific package (repeatable, accepts aliases).");
        Console.WriteLine("  --all               Target all packages.");
        Console.WriteLine("  --changed-only      Target packages with detected repository changes (default).");
        Console.WriteLine("  --dry-run           Preview actions without writing files or publishing.");
        Console.WriteLine("  --set-version <x.y.z>  Explicitly set the next version.");
        Console.WriteLine("  --bump <patch|minor|major>  Version bump increment (defaults to patch).");
        Console.WriteLine("  --print-version    Emit the resolved version to stdout (bump only).");
        Console.WriteLine("  --interactive       Prompt before executing critical steps (future use).");
        Console.WriteLine("  --since <ref>       Detect changes relative to the provided git reference.");
        Console.WriteLine();
        Console.WriteLine("Legacy aliases --frontend/--test/--backend/--both remain supported for compatibility.");
    }
}
