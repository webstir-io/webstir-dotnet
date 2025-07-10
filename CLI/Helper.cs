using CLI.Models;
using static CLI.Constants.Commands;

namespace CLI;

public static class Helper
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
            ? $"{Webstir} {name} {usageParams}" 
            : $"{Webstir} {name}";

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
            $"Initialize a new {Webstir} project",
            [
                Example($"{Webstir} {InitCommand}", "Create a full-stack project (default)"),
                Example($"{Webstir} {InitCommand} {ClientOnlyOption}", "Create a client-only project"),
                Example($"{Webstir} {InitCommand} {ServerOnlyOption}", "Create a server-only project")
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
                Example($"{Webstir} {AddPageCommand} home", "Create a new home page"),
                Example($"{Webstir} {AddPageCommand} about", "Create a new about page")
            ],
            null,
            "<page-name>");

    private static CommandHelp GetBuildCommand() => 
        CreateCommand(BuildCommand, 
            "Build the project once",
            [
                Example($"{Webstir} {BuildCommand}", "Build the project"),
                Example($"{Webstir} {BuildCommand} {CleanOption}", "Clean build (removes build directory first)")
            ],
            [
                Option(CleanOption, "Clean build directory before building")
            ],
            "[options]");

    private static CommandHelp GetWatchCommand() => 
        CreateCommand(WatchCommand, 
            "Build and watch for changes (default)",
            [
                Example($"{Webstir} {WatchCommand}", "Start development server with hot reload"),
                Example(Webstir, $"Same as '{Webstir} {WatchCommand}'")
            ]);

    private static CommandHelp GetPublishCommand() => 
        CreateCommand(PublishCommand, 
            "Create production build",
            [
                Example($"{Webstir} {PublishCommand}", "Create optimized production build")
            ]);

    private static CommandHelp GetHelpCommand() => 
        CreateCommand(HelpCommand, 
            "Show help information",
            [
                Example($"{Webstir} {HelpCommand}", "Show general help"),
                Example($"{Webstir} {HelpCommand} {InitCommand}", "Show help for init command")
            ],
            null,
            "[command]");

    private static CommandHelp GetDemoCommand() => 
        CreateCommand(DemoCommand, 
            "Create a demo application showcasing all webstir features",
            [
                Example($"{Webstir} {DemoCommand}", "Create a demo app in the current directory"),
                Example($"{Webstir} {DemoCommand} my-demo", "Create a demo app in 'my-demo' directory")
            ],
            null,
            "[directory]");

    public static void ShowGeneralHelp()
    {
        Console.WriteLine($"{Webstir} - Modern web development build tool");
        Console.WriteLine();
        Console.WriteLine($"Usage: {Webstir} [command] [options]");
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
        Console.WriteLine($"Run '{Webstir} {HelpCommand} <command>' for more information on a specific command.");
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