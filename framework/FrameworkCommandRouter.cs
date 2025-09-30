namespace Framework;

using System;
using System.Threading.Tasks;
using Framework.Commands;
using Microsoft.Extensions.Logging;

internal sealed class FrameworkCommandRouter
{
    private readonly ILogger<FrameworkCommandRouter> _logger;
    private readonly PackageConsoleCommand _packages;

    public FrameworkCommandRouter(ILogger<FrameworkCommandRouter> logger, PackageConsoleCommand packages)
    {
        _logger = logger;
        _packages = packages;
    }

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
        || value.Equals("verify", StringComparison.OrdinalIgnoreCase);

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
        Console.WriteLine("Usage: framework packages [sync|publish|verify] [options]");
        Console.WriteLine("       framework packages --help");
        Console.WriteLine("       framework sync [options] (shorthand)\n");
    }

    private int ShowPackagesUsage()
    {
        Console.WriteLine("framework packages <sync|publish|verify> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  sync       Rebuild framework packages (default).");
        Console.WriteLine("  publish    Rebuild and publish packages missing from the registry.");
        Console.WriteLine("  verify     Ensure manifests and tarballs are committed.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --frontend     Rebuild only the frontend package.");
        Console.WriteLine("  --test         Rebuild only the testing package.");
        Console.WriteLine("  --both         Rebuild both packages (default).");
        Console.WriteLine("  --publish      Publish tarballs after syncing.");
        Console.WriteLine("  --verify       After syncing, ensure git status is clean.");
        Console.WriteLine("  --help, -h     Show this message.");
        return 0;
    }
}
