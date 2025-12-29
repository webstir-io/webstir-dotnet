using System;
using System.Threading.Tasks;
using Framework.Commands;
using Microsoft.Extensions.Logging;

namespace Framework;

internal sealed class Runner(ILogger<Runner> logger, PackageConsoleCommand packages)
{
    private readonly ILogger<Runner> _logger = logger;
    private readonly PackageConsoleCommand _packages = packages;

    public Task<int> ExecuteAsync(string[] args)
    {
        // Normalize leading "--" which dotnet may pass through as the first argument
        if (args.Length > 0 && string.Equals(args[0], "--", StringComparison.Ordinal))
        {
            args = args.Length > 1 ? args[1..] : Array.Empty<string>();
        }

        if (args.Length > 0 && string.Equals(args[0], "--", StringComparison.Ordinal))
        {
            args = args.Length > 1 ? args[1..] : Array.Empty<string>();
        }

        if (args.Length == 0)
        {
            ShowUsage();
            return _packages.ExecuteAsync(Array.Empty<string>());
        }

        if (ContainsHelp(args))
        {
            return Task.FromResult(ShowPackagesUsage());
        }

        string command = args[0];
        string[] remaining = args.Length > 1 ? args[1..] : Array.Empty<string>();

        if (IsPackagesCommand(command))
        {
            return ContainsHelp(remaining) ? Task.FromResult(ShowPackagesUsage()) : _packages.ExecuteAsync(remaining);
        }

        if (IsPackagesSubcommand(command))
        {
            return ContainsHelp(args[1..]) ? Task.FromResult(ShowPackagesUsage()) : _packages.ExecuteAsync(args);
        }

        _logger.LogError("Unknown command '{Command}'. Use 'packages'.", command);
        ShowUsage();
        return Task.FromResult(1);
    }

    private static bool IsPackagesCommand(string value) =>
        value.Equals("packages", StringComparison.OrdinalIgnoreCase)
        || value.Equals("package", StringComparison.OrdinalIgnoreCase);

    private static bool IsPackagesSubcommand(string value) =>
        value.Equals("sync", StringComparison.OrdinalIgnoreCase)
        || value.Equals("publish", StringComparison.OrdinalIgnoreCase)
        || value.Equals("verify", StringComparison.OrdinalIgnoreCase)
        || value.Equals("diff", StringComparison.OrdinalIgnoreCase)
        || value.Equals("bump", StringComparison.OrdinalIgnoreCase)
        || value.Equals("release", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsHelp(string[] args)
    {
        foreach (string value in args)
        {
            if (IsHelp(value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHelp(string value) =>
        value.Equals("help", StringComparison.OrdinalIgnoreCase)
        || value.Equals("--help", StringComparison.OrdinalIgnoreCase)
        || value.Equals("-h", StringComparison.OrdinalIgnoreCase);

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: framework packages [bump|sync|release|publish|verify|diff] [options]");
        Console.WriteLine("       framework packages --help");
        Console.WriteLine("       framework sync [options] (shorthand)\n");
    }

    private int ShowPackagesUsage()
    {
        Console.WriteLine("framework packages <bump|sync|release|publish|verify|diff> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  bump       Update package versions.");
        Console.WriteLine("  sync       Rebuild framework packages and refresh registry metadata (default).");
        Console.WriteLine("  release    Bump and rebuild packages without publishing.");
        Console.WriteLine("  publish    Bump, rebuild, and publish packages to the configured registry.");
        Console.WriteLine("  verify     Validate registry metadata and template dependencies.");
        Console.WriteLine("  diff       Report registry metadata differences without modifying files.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --package <name>   Target specific packages (repeatable).");
        Console.WriteLine("  --all             Target all packages (default).");
        Console.WriteLine("  --dry-run         Preview actions without writing files or publishing.");
        Console.WriteLine("  --set-version     Explicitly set the next version (bump/release/publish).");
        Console.WriteLine("  --bump            Choose version bump (patch|minor|major). Defaults to patch.");
        Console.WriteLine("  --print-version   Emit the resolved version to stdout (bump only).");
        Console.WriteLine("  --frontend/--test legacy shortcuts remain available.");
        Console.WriteLine("  --help, -h        Show this message.");
        return 0;
    }
}
