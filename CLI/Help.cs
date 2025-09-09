using System;
using System.Collections.Generic;
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

    private static CommandHelp GetTestCommand() =>
        CreateCommand(Commands.Test,
            "Run tests and print a summary",
            [
                Example($"{App.Name} {Commands.Test}", "Build (incremental) and run tests"),
                Example($"{App.Name} {Commands.Test} ./my-app", "Run in ./my-app")
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
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {cmd.Name,-12}");
            Console.ResetColor();
            Console.WriteLine($"  {cmd.Description}");
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
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"  {App.Name} build ./my-project         # Build project in ./my-project directory");
        Console.WriteLine($"  {App.Name} watch /path/to/project     # Watch project at absolute path");
        Console.WriteLine($"  {App.Name} init new-app               # Initialize new project in new-app directory");
        Console.ResetColor();
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
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"  {option.Name,-18}");
                Console.ResetColor();
                Console.WriteLine($"{option.Description}");
            }
        }

        if (command.Examples.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Examples:");
            foreach (string example in command.Examples)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"  {example}");
                Console.ResetColor();
            }
        }
    }
}
