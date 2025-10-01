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
        || value.Equals("diff", StringComparison.OrdinalIgnoreCase);

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
        Console.WriteLine("Usage: framework packages [sync|publish|verify|diff] [options]");
        Console.WriteLine("       framework packages --help");
        Console.WriteLine("       framework sync [options] (shorthand)\n");
    }

    private int ShowPackagesUsage()
    {
        Console.WriteLine("framework packages <sync|publish|verify|diff> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  sync       Build framework packages (default).");
        Console.WriteLine("  publish    Build and publish packages to the configured registry.");
        Console.WriteLine("  verify     Validate tarball metadata and embedded assets.");
        Console.WriteLine("  diff       Repack tarballs and report checksum/size differences without modifying files.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --frontend     Rebuild only the frontend package.");
        Console.WriteLine("  --test         Rebuild only the testing package.");
        Console.WriteLine("  --both         Rebuild both packages (default).");
        Console.WriteLine("  --prune-webstir  Remove cached .webstir tarballs under Tests/out and CLI/out (sync only).");
        Console.WriteLine("  --help, -h     Show this message.");
        return 0;
    }
}
