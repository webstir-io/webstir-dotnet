using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine;
using Engine.Models;
using Engine.Helpers;

namespace CLI;

public static class Help
{
    private const string RouteDocsUrl = "https://github.com/webstir-io/webstir-portal/blob/main/docs/reference/cli.md#add-route";
    private const string JobDocsUrl = "https://github.com/webstir-io/webstir-portal/blob/main/docs/reference/cli.md#add-job";
    private const string SchemaReferenceDocsUrl = "https://github.com/webstir-io/module-contract#schema-references";

    private static readonly string SchemaReferenceHint = $"kind:name@source (see {SchemaReferenceDocsUrl})";

    private static readonly Dictionary<string, CommandHelp> AppCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        [Commands.Init] = GetInitCommand(),
        [Commands.Repair] = GetRepairCommand(),
        [Commands.AddPage] = GetAddPageCommand(),
        [Commands.AddTest] = GetAddTestCommand(),
        [Commands.AddRoute] = GetAddRouteCommand(),
        [Commands.AddJob] = GetAddJobCommand(),
        [Commands.Enable] = GetEnableCommand(),
        [Commands.Build] = GetBuildCommand(),
        [Commands.Test] = GetTestCommand(),
        [Commands.Watch] = GetWatchCommand(),
        [Commands.BackendInspect] = GetBackendInspectCommand(),
        [Commands.Install] = GetInstallCommand(),
        [Commands.Publish] = GetPublishCommand(),
        [Commands.Smoke] = GetSmokeCommand(),
        [Commands.Help] = GetHelpCommand()
    };

    private static CommandHelp CreateCommand(
        string name,
        string description,
        string[]? examples = null,
        CommandOption[]? options = null,
        string? usageParams = null)
    {
        string usage = usageParams != null
            ? $"{App.Name} {name} {usageParams}"
            : $"{App.Name} {name}";

        return new CommandHelp
        {
            Name = name,
            Description = description,
            Usage = usage,
            Examples = examples?.ToList() ?? [],
            Options = options?.ToList() ?? []
        };
    }

    private static CommandOption Option(string name, string description) =>
        new()
        {
            Name = name,
            Description = description
        };

    private static string Example(string command, string description) =>
        $"{command,-40}# {description}";

    private static CommandHelp GetInitCommand() =>
        CreateCommand(Commands.Init,
            $"Initialize a new {App.Name} project",
            [
                Example($"{App.Name} {Commands.Init} {InitModes.Full} my-app", "Create a full-stack app (frontend + backend)"),
                Example($"{App.Name} {Commands.Init} {InitModes.Ssg} docs", "Create a static site (SSG) project"),
                Example($"{App.Name} {Commands.Init} {InitModes.Spa} dashboard", "Create a SPA frontend project"),
                Example($"{App.Name} {Commands.Init} {InitModes.Api} api", "Create a backend-only API project"),
                Example($"{App.Name} {Commands.Init} my-app", "Create a full-stack app named 'my-app' (default)")
            ],
            [
                Option(ProjectOptions.ProjectName, "Specify target project folder name (alternative to positional <directory>)")
            ],
            "<mode> <directory> | <directory>");

    private static CommandHelp GetRepairCommand() =>
        CreateCommand(Commands.Repair,
            "Restore missing scaffold files for the current workspace mode (does not overwrite existing files)",
            [
                Example($"{App.Name} {Commands.Repair}", "Restore missing files in the current project"),
                Example($"{App.Name} {Commands.Repair} ./my-app", "Restore missing files for ./my-app")
            ],
            [
                Option(RepairOptions.DryRun, "Print what would be restored without writing any files")
            ],
            "[<path>]");

    private static CommandHelp GetAddPageCommand() =>
        CreateCommand(Commands.AddPage,
            "Add a new page (frontend only)",
            [
                Example($"{App.Name} {Commands.AddPage} about", "Create a new about page (defaults to JS-free scaffold when webstir.mode=ssg)")
            ],
            null,
            "<page-name>");

    private static CommandHelp GetBuildCommand() =>
        CreateCommand(Commands.Build,
            "Build the project once",
            [
                Example($"{App.Name} {Commands.Build}", "Build the project"),
                Example($"WEBSTIR_FRONTEND_PROVIDER_SPEC=../webstir-frontend {App.Name} {Commands.Build}", "Build with a local frontend provider"),
                Example($"WEBSTIR_BACKEND_PROVIDER_SPEC=../webstir-backend {App.Name} {Commands.Build} {TestOptions.Runtime} backend", "Build with a local backend provider"),
                Example($"{App.Name} {Commands.Build} {BuildOptions.Clean}", "Clean build (removes build directory first)"),
                Example($"{App.Name} {Commands.Build} ./my-app", "Build project in ./my-app directory"),
                Example($"{App.Name} {Commands.Build} {TestOptions.Runtime} backend", "Focus on backend workers only")
            ],
            [
                Option(BuildOptions.Clean, "Clean build directory before building"),
                Option($"{TestOptions.Runtime} | {TestOptions.RuntimeShort}", "Limit build to frontend, backend, or all (default)")
            ],
            "[options]");

    private static CommandHelp GetInstallCommand() =>
        CreateCommand(Commands.Install,
            "Synchronize framework package dependencies from the registry",
            [
                Example($"{App.Name} {Commands.Install}", "Install pinned frontend/testing/backend packages"),
                Example($"{App.Name} {Commands.Install} ./my-app", "Synchronize packages for ./my-app"),
                Example($"{App.Name} {Commands.Install} {InstallOptions.DryRun}", "Preview actions without running a package install"),
                Example($"{App.Name} {Commands.Install} {InstallOptions.Clean}", "Clear cached workspace packages before installing"),
                Example($"{App.Name} {Commands.Install} {InstallOptions.PackageManager}=pnpm@10.5.2", "Run installs with a specific package manager version via Corepack")
            ],
            [
                Option(InstallOptions.DryRun, "Report pending changes without running a package install"),
                Option(InstallOptions.Clean, "Remove cached workspace packages before reinstalling"),
                Option($"{InstallOptions.PackageManager} | {InstallOptions.PackageManagerShort}", "Override the package manager for this run (npm, pnpm, yarn, optional @version)")
            ],
            "[options]");

    private static CommandHelp GetTestCommand() =>
        CreateCommand(Commands.Test,
            "Run tests through the configured provider (defaults to @webstir-io/webstir-testing)",
            [
                Example($"{App.Name} {Commands.Test}", "Build (incremental) and run tests"),
                Example($"{App.Name} {Commands.Test} ./my-app", "Run in ./my-app"),
                Example($"WEBSTIR_TESTING_PROVIDER=@webstir-io/vitest-testing {App.Name} {Commands.Test}", "Execute through the Vitest provider"),
                Example($"WEBSTIR_TESTING_PROVIDER_SPEC=../vitest-testing {App.Name} {Commands.Test}", "Install and run against a local provider build"),
                Example($"{App.Name} {Commands.Test} {TestOptions.Runtime} backend", "Run backend-only tests (skips frontend tests)")
            ],
            [
                Option($"{TestOptions.Runtime} | {TestOptions.RuntimeShort}", "Limit tests to a runtime: frontend, backend, or all (default)")
            ]);


    private static CommandHelp GetWatchCommand() =>
        CreateCommand(Commands.Watch,
            "Build and watch for changes (default)",
            [
                Example($"{App.Name} {Commands.Watch}", "Start development server with hot reload"),
                Example(App.Name, $"Same as '{App.Name} {Commands.Watch}'"),
                Example($"{App.Name} {Commands.Watch} ../project", "Watch project in parent directory")
            ],
            [
                Option($"{TestOptions.Runtime} | {TestOptions.RuntimeShort}", "Limit watch-triggered tests to frontend, backend, or all (default)")
            ]);

    private static CommandHelp GetBackendInspectCommand() =>
        CreateCommand(Commands.BackendInspect,
            "Build backend (server-only) and print manifest metadata",
            [
                Example($"{App.Name} {Commands.BackendInspect}", "Inspect backend manifest in current project"),
                Example($"{App.Name} {Commands.BackendInspect} ./api", "Inspect a sibling project"),
                Example($"{App.Name} {Commands.BackendInspect} {ProjectOptions.ProjectName} api", "Select a specific project when multiple exist")
            ],
            [
                Option(ProjectOptions.ProjectName, "Select workspace project when multiple exist")
            ],
            "[project]");

    private static CommandHelp GetPublishCommand() =>
        CreateCommand(Commands.Publish,
            "Create production build",
            [
                Example($"{App.Name} {Commands.Publish}", "Create optimized production build"),
                Example($"{App.Name} {Commands.Publish} {TestOptions.Runtime} backend", "Only publish backend artifacts"),
                Example($"{App.Name} {Commands.Publish} --frontend-mode ssg", "Publish static frontend assets (SSG preview)")
            ],
            [
                Option($"{TestOptions.Runtime} | {TestOptions.RuntimeShort}", "Limit publish work to frontend, backend, or all (default)"),
                Option(FrontendModeParser.FrontendMode, "Frontend publish mode: bundle or ssg (defaults to ssg when webstir.mode=ssg)")
            ]);

    private static CommandHelp GetSmokeCommand() =>
        CreateCommand(Commands.Smoke,
            "Run the accounts example through the CLI and report backend manifest routes",
            [
                Example($"{App.Name} {Commands.Smoke}", "Copy the accounts example and verify manifest ingestion"),
                Example($"{App.Name} {Commands.Smoke} ./workspaces/accounts", "Use an existing workspace instead of copying the example")
            ],
            null,
            "[workspace]");

    private static CommandHelp GetAddTestCommand() =>
        CreateCommand(Commands.AddTest,
            "Scaffold a starter test",
            [
                Example($"{App.Name} {Commands.AddTest} example", "Create src/tests/example.test.ts"),
                Example($"{App.Name} {Commands.AddTest} frontend/app/pages/home/sometest", "Create src/frontend/app/pages/home/tests/sometest.test.ts")
            ],
            null,
            "<name-or-path>");

    private static CommandHelp GetAddRouteCommand() =>
        CreateCommand(Commands.AddRoute,
            $"Add a backend route entry to the module manifest (package.json). Metadata and schema flags are documented at {RouteDocsUrl}",
            [
                Example($"{App.Name} {Commands.AddRoute} users", "Add GET /api/users to webstir.moduleManifest.routes"),
                Example($"{App.Name} {Commands.AddRoute} users --method POST --path /api/users", "Add POST /api/users route"),
                Example($"{App.Name} {Commands.AddRoute} accounts --fastify", "Also scaffold a Fastify handler under src/backend/server/routes/"),
                Example($"{App.Name} {Commands.AddRoute} users --project api", "Target a specific workspace project when multiple exist"),
                Example($"{App.Name} {Commands.AddRoute} users --params-schema zod:UserParams@src/shared/contracts/users.ts", "Attach schema references via kind:name@source format")
            ],
            [
                Option("--method", "HTTP method (default GET)"),
                Option("--path", "Route path (default /api/<name>)"),
                Option(ProjectOptions.ProjectName, "Select workspace project when multiple exist"),
                Option("--fastify", "Also scaffold a Fastify handler and register it if possible"),
                Option("--summary", "Short manifest summary (stored on webstir.moduleManifest.routes)"),
                Option("--description", "Longer manifest description for docs and tooling"),
                Option("--tags", "Comma-separated tags (trimmed and deduped case-insensitively)"),
                Option("--params-schema", $"Schema reference for params ({SchemaReferenceHint})"),
                Option("--query-schema", $"Schema reference for query ({SchemaReferenceHint})"),
                Option("--body-schema", $"Schema reference for body ({SchemaReferenceHint})"),
                Option("--headers-schema", $"Schema reference for headers ({SchemaReferenceHint})"),
                Option("--response-schema", $"Schema reference for response body ({SchemaReferenceHint})"),
                Option("--response-status", "Override the success status code (100-599)"),
                Option("--response-headers-schema", $"Schema reference for response headers ({SchemaReferenceHint})")
            ],
            "<name> [--method <METHOD>] [--path <path>] [--fastify] [--project <project>]");

    private static CommandHelp GetAddJobCommand() =>
        CreateCommand(Commands.AddJob,
            $"Add a backend job stub and manifest entry. Metadata flags are documented at {JobDocsUrl}",
            [
                Example($"{App.Name} {Commands.AddJob} cleanup", "Create src/backend/jobs/cleanup/index.ts and add to manifest"),
                Example($"{App.Name} {Commands.AddJob} nightly --schedule \"0 0 * * *\"", "Add a cron-like schedule to the manifest entry"),
                Example($"{App.Name} {Commands.AddJob} cleanup --project api", "Target a specific workspace project"),
                Example($"{App.Name} {Commands.AddJob} nightly --description \"Archive data\" --priority 5", "Store manifest metadata for docs and alerting")
            ],
            [
                Option("--schedule", "Optional schedule string stored verbatim in the manifest"),
                Option("--description", "Manifest description (surfaced in docs/help output)"),
                Option("--priority", "Manifest priority (numbers stored as integers, otherwise as strings)"),
                Option(ProjectOptions.ProjectName, "Select workspace project when multiple exist")
            ],
            "<name> [--schedule <expression>] [--project <project>]");

    private static CommandHelp GetEnableCommand() =>
        CreateCommand(Commands.Enable,
            "Enable optional capabilities in an existing workspace.",
            [
                Example($"{App.Name} {Commands.Enable} scripts home", "Add a page-level script stub to pages/home."),
                Example($"{App.Name} {Commands.Enable} spa", "Enable client routing and router assets."),
                Example($"{App.Name} {Commands.Enable} client-nav", "Enable PJAX-style navigation helper."),
                Example($"{App.Name} {Commands.Enable} search", "Enable site-wide search UI and index output."),
                Example($"{App.Name} {Commands.Enable} backend", "Add backend scaffold to a frontend-only app.")
            ],
            null,
            "<scripts <page>|spa|client-nav|search|backend>");

    private static CommandHelp GetHelpCommand() =>
        CreateCommand(Commands.Help,
            "Show help information",
            [
                Example($"{App.Name} {Commands.Help}", "Show general help"),
                Example($"{App.Name} {Commands.Help} {Commands.Init}", "Show help for init command")
            ],
            null,
            "[command]");

    // Demo command temporarily removed

    public static void ShowGeneralHelp()
    {
        Console.WriteLine($"{App.Name} - Modern web development build tool");
        Console.WriteLine();
        Console.WriteLine($"Usage: {App.Name} [command] [options] [path]");
        Console.WriteLine();
        Console.WriteLine("Commands:");

        foreach (CommandHelp cmd in AppCommands.Values.OrderBy(c => c.Name))
        {
            WriteCommandEntry(cmd);
        }

        Console.WriteLine();
        Console.WriteLine($"Run '{App.Name} {Commands.Help} <command>' for more information on a specific command.");
        Console.WriteLine();
        Console.WriteLine("Path parameter:");
        Console.WriteLine("  You can specify a path as the last argument to run commands in a different directory.");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  Workers are injected (IWorkflowWorker); 'add-page' targets the frontend worker.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        WriteExampleLine($"{App.Name} build ./my-project         # Build project in ./my-project directory");
        WriteExampleLine($"{App.Name} watch /path/to/project     # Watch project at absolute path");
        WriteExampleLine($"{App.Name} init new-app               # Initialize new project in new-app directory");
        WriteExampleLine($"WEBSTIR_TESTING_PROVIDER=@webstir-io/vitest-testing {App.Name} test   # Run tests with the Vitest provider");
        WriteExampleLine($"{App.Name} install                    # Sync registry packages and providers");
        WriteExampleLine($"{App.Name} test --help               # See provider override guidance");
        WriteExampleLine($"{App.Name} smoke                    # Run the accounts smoke check and report manifest routes");
    }

    public static void ShowCommandHelp(string commandName)
    {
        ArgumentNullException.ThrowIfNull(commandName);

        if (!AppCommands.TryGetValue(commandName, out CommandHelp? command))
        {
            Console.WriteLine($"Unknown command '{commandName}'");
            Console.WriteLine();
            ShowGeneralHelp();
            return;
        }

        Console.WriteLine(command.Description);
        Console.WriteLine();
        Console.WriteLine($"Usage: {command.Usage}");

        if (command.Options.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Options:");
            foreach (CommandOption option in command.Options)
            {
                WriteOptionEntry(option);
            }
        }

        if (command.Examples.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Examples:");
            foreach (string example in command.Examples)
            {
                WriteExampleLine(example);
            }
        }
    }

    private static bool TryWriteWithColor(ConsoleColor color, Action action)
    {
        if (Console.IsOutputRedirected || Console.IsErrorRedirected)
        {
            return false;
        }

        try
        {
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            action();
            Console.ForegroundColor = previous;
            return true;
        }
        catch (IOException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }

        return false;
    }

    private static void WriteCommandEntry(CommandHelp cmd)
    {
        if (TryWriteWithColor(ConsoleColor.Cyan, () => Console.Write($"  {cmd.Name,-12}")))
        {
            Console.WriteLine($"  {cmd.Description}");
        }
        else
        {
            Console.WriteLine($"  {cmd.Name,-12}  {cmd.Description}");
        }
    }

    private static void WriteOptionEntry(CommandOption option)
    {
        if (TryWriteWithColor(ConsoleColor.Yellow, () => Console.Write($"  {option.Name,-18}")))
        {
            Console.WriteLine($"{option.Description}");
        }
        else
        {
            Console.WriteLine($"  {option.Name,-18}  {option.Description}");
        }
    }

    private static void WriteExampleLine(string text)
    {
        if (!TryWriteWithColor(ConsoleColor.Gray, () => Console.WriteLine($"  {text}")))
        {
            Console.WriteLine($"  {text}");
        }
    }
}
