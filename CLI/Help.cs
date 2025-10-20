using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine;
using Engine.Models;

namespace CLI;

public static class Help
{
    private static readonly Dictionary<string, CommandHelp> AppCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        [Commands.Init] = GetInitCommand(),
        [Commands.AddPage] = GetAddPageCommand(),
        [Commands.AddTest] = GetAddTestCommand(),
        [Commands.Build] = GetBuildCommand(),
        [Commands.Test] = GetTestCommand(),
        [Commands.Watch] = GetWatchCommand(),
        [Commands.Install] = GetInstallCommand(),
        [Commands.Publish] = GetPublishCommand(),
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
                Example($"{App.Name} {Commands.Init}", "Create a full-stack project (default)"),
                Example($"{App.Name} {Commands.Init} my-app", "Create a project in 'my-app' directory (positional)"),
                Example($"{App.Name} {Commands.Init} --project-name my-app", "Create a project in 'my-app' directory (flag)"),
                Example($"{App.Name} {Commands.Init} {InitOptions.ClientOnly}", "Create a client-only project"),
                Example($"{App.Name} {Commands.Init} {InitOptions.ServerOnly}", "Create a server-only project")
            ],
            [
                Option(InitOptions.ClientOnly, "Create a client-side only project"),
                Option(InitOptions.ServerOnly, "Create a server-side only project"),
                Option(ProjectOptions.ProjectName, "Specify target project folder name (alternative to positional [directory])")
            ],
            "[options] [directory]");

    private static CommandHelp GetAddPageCommand() =>
        CreateCommand(Commands.AddPage,
            "Add a new page (frontend only)",
            [
                Example($"{App.Name} {Commands.AddPage} about", "Create a new about page")
            ],
            null,
            "<page-name>");

    private static CommandHelp GetBuildCommand() =>
        CreateCommand(Commands.Build,
            "Build the project once",
            [
                Example($"{App.Name} {Commands.Build}", "Build the project"),
                Example($"{App.Name} {Commands.Build} {BuildOptions.Clean}", "Clean build (removes build directory first)"),
                Example($"{App.Name} {Commands.Build} ./my-app", "Build project in ./my-app directory")
            ],
            [
                Option(BuildOptions.Clean, "Clean build directory before building")
            ],
            "[options]");

    private static CommandHelp GetInstallCommand() =>
        CreateCommand(Commands.Install,
            "Synchronize framework package dependencies",
            [
                Example($"{App.Name} {Commands.Install}", "Install bundled frontend/test packages"),
                Example($"{App.Name} {Commands.Install} ./my-app", "Synchronize packages for ./my-app"),
                Example($"{App.Name} {Commands.Install} {InstallOptions.DryRun}", "Preview actions without running npm install"),
                Example($"{App.Name} {Commands.Install} {InstallOptions.Clean}", "Clear cached workspace packages before installing")
            ],
            [
                Option(InstallOptions.DryRun, "Report pending changes without running npm install"),
                Option(InstallOptions.Clean, "Remove cached workspace packages before reinstalling")
            ]);

    private static CommandHelp GetTestCommand() =>
        CreateCommand(Commands.Test,
            "Run tests through the configured provider (defaults to @webstir-io/webstir-testing)",
            [
                Example($"{App.Name} {Commands.Test}", "Build (incremental) and run tests"),
                Example($"{App.Name} {Commands.Test} ./my-app", "Run in ./my-app"),
                Example($"WEBSTIR_TESTING_PROVIDER=@webstir-io/vitest-testing {App.Name} {Commands.Test}", "Execute through the Vitest provider")
            ]);

    private static CommandHelp GetWatchCommand() =>
        CreateCommand(Commands.Watch,
            "Build and watch for changes (default)",
            [
                Example($"{App.Name} {Commands.Watch}", "Start development server with hot reload"),
                Example(App.Name, $"Same as '{App.Name} {Commands.Watch}'"),
                Example($"{App.Name} {Commands.Watch} ../project", "Watch project in parent directory")
            ]);

    private static CommandHelp GetPublishCommand() =>
        CreateCommand(Commands.Publish,
            "Create production build",
            [
                Example($"{App.Name} {Commands.Publish}", "Create optimized production build")
            ]);

    private static CommandHelp GetAddTestCommand() =>
        CreateCommand(Commands.AddTest,
            "Scaffold a starter test",
            [
                Example($"{App.Name} {Commands.AddTest} example", "Create src/tests/example.test.ts"),
                Example($"{App.Name} {Commands.AddTest} frontend/app/pages/home/sometest", "Create src/frontend/app/pages/home/tests/sometest.test.ts")
            ],
            null,
            "<name-or-path>");

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
