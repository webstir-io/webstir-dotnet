using Engine.Models;
using static CLI.Constants;

namespace CLI;

public static class Help
{
    private static readonly Dictionary<string, CommandHelp> Commands = new()
    {
    
        [InitCommand] = GetInitCommand(),
        [AddPageCommand] = GetAddPageCommand(),
        [BuildCommand] = GetBuildCommand(),
        [WatchCommand] = GetWatchCommand(),
        [PublishCommand] = GetPublishCommand(),
        [HelpCommand] = GetHelpCommand(),
        [DemoCommand] = GetDemoCommand()
    };

    private static CommandHelp CreateCommand(
        string name, 
        string description, 
        string[]? examples = null, 
        CommandOption[]? options = null,
        string? usageParams = null)
    {
        var usage = usageParams != null 
            ? $"{AppName} {name} {usageParams}" 
            : $"{AppName} {name}";

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
        new() { Name = name, Description = description };

    private static string Example(string command, string description) => 
        $"{command,-40}# {description}";

    private static CommandHelp GetInitCommand() => 
        CreateCommand(InitCommand, 
            $"Initialize a new {AppName} project",
            [
                Example($"{AppName} {InitCommand}", "Create a full-stack project (default)"),
                Example($"{AppName} {InitCommand} {ClientOnlyOption}", "Create a client-only project"),
                Example($"{AppName} {InitCommand} {ServerOnlyOption}", "Create a server-only project")
            ],
            [
                Option(ClientOnlyOption, "Create a client-side only project"),
                Option(ServerOnlyOption, "Create a server-side only project")
            ],
            "[options]");

    private static CommandHelp GetAddPageCommand() => 
        CreateCommand(AddPageCommand, 
            "Add a new page to your project",
            [
                Example($"{AppName} {AddPageCommand} home", "Create a new home page"),
                Example($"{AppName} {AddPageCommand} about", "Create a new about page")
            ],
            null,
            "<page-name>");

    private static CommandHelp GetBuildCommand() => 
        CreateCommand(BuildCommand, 
            "Build the project once",
            [
                Example($"{AppName} {BuildCommand}", "Build the project"),
                Example($"{AppName} {BuildCommand} {CleanOption}", "Clean build (removes build directory first)"),
                Example($"{AppName} {BuildCommand} ./my-app", "Build project in ./my-app directory")
            ],
            [
                Option(CleanOption, "Clean build directory before building")
            ],
            "[options]");

    private static CommandHelp GetWatchCommand() => 
        CreateCommand(WatchCommand, 
            "Build and watch for changes (default)",
            [
                Example($"{AppName} {WatchCommand}", "Start development server with hot reload"),
                Example(AppName, $"Same as '{AppName} {WatchCommand}'"),
                Example($"{AppName} {WatchCommand} ../project", "Watch project in parent directory")
            ]);

    private static CommandHelp GetPublishCommand() => 
        CreateCommand(PublishCommand, 
            "Create production build",
            [
                Example($"{AppName} {PublishCommand}", "Create optimized production build")
            ]);

    private static CommandHelp GetHelpCommand() => 
        CreateCommand(HelpCommand, 
            "Show help information",
            [
                Example($"{AppName} {HelpCommand}", "Show general help"),
                Example($"{AppName} {HelpCommand} {InitCommand}", "Show help for init command")
            ],
            null,
            "[command]");

    private static CommandHelp GetDemoCommand() => 
        CreateCommand(DemoCommand, 
            "Create a demo application showcasing all webstir features",
            [
                Example($"{AppName} {DemoCommand}", "Create a demo app in the current directory"),
                Example($"{AppName} {DemoCommand} my-demo", "Create a demo app in 'my-demo' directory")
            ],
            null,
            "[directory]");

    public static void ShowGeneralHelp()
    {
        Console.WriteLine($"{AppName} - Modern web development build tool");
        Console.WriteLine();
        Console.WriteLine($"Usage: {AppName} [command] [options] [path]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        
        foreach (var cmd in Commands.Values.OrderBy(c => c.Name))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {cmd.Name,-12}");
            Console.ResetColor();
            Console.WriteLine($"  {cmd.Description}");
        }
        
        Console.WriteLine();
        Console.WriteLine($"Run '{AppName} {HelpCommand} <command>' for more information on a specific command.");
        Console.WriteLine();
        Console.WriteLine("Path parameter:");
        Console.WriteLine("  You can specify a path as the last argument to run commands in a different directory.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"  {AppName} build ./my-project         # Build project in ./my-project directory");
        Console.WriteLine($"  {AppName} watch /path/to/project     # Watch project at absolute path");
        Console.WriteLine($"  {AppName} init new-app               # Initialize new project in new-app directory");
        Console.ResetColor();
    }

    public static void ShowCommandHelp(string commandName)
    {
        if (!Commands.TryGetValue(commandName.ToLower(), out var command))
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
            foreach (var option in command.Options)
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
            foreach (var example in command.Examples)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"  {example}");
                Console.ResetColor();
            }
        }
    }
}