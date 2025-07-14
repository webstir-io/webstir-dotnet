using Engine.Services;
using Microsoft.Extensions.DependencyInjection;
using Engine;

namespace CLI;

public class Runner(IServiceProvider serviceProvider)
{
    private App _app = null!;
    private IWorkflowFactory _workflowFactory = null!;
        
    public async Task Run(string[] args)
    {
        var command = args.Length != 0
            ? args.First()
            : string.Empty;

        if (IsHelpRequested(command, args))
            return;

        var (remainingArgs, workingDirectory) = ExtractWorkingDirectory(args);

        using var scope = serviceProvider.CreateScope();
        _app = scope.ServiceProvider.GetRequiredService<App>();
        _workflowFactory = scope.ServiceProvider.GetRequiredService<IWorkflowFactory>();
        _app.Initialize(workingDirectory);

        await ExecuteCommand(command, remainingArgs);
    }

    private static (string[] args, string workingDirectory) ExtractWorkingDirectory(string[] args)
    {
        // Check if the last argument is a path (not starting with -- or -)
        if (args.Length > 0)
        {
            var lastArg = args[args.Length - 1];
            
            // If it's not an option and it's not a known command, treat it as a path
            if (!lastArg.StartsWith('-') && args.Length > 1)
            {
                return (args.Take(args.Length - 1).ToArray(), lastArg);
            }
        }
        
        return (args, Directory.GetCurrentDirectory());
    }

    private static bool IsHelpRequested(string command, string[] args)
    {
        if (command == App.Commands.Help || command == App.Options.Help || command == App.Options.HelpShort)
        {
            if (args.Length > 1 && command == App.Commands.Help)
                Help.ShowCommandHelp(args[1]);
            else
                Help.ShowGeneralHelp();
            return true;
        }

        if (args.Length > 1 && (args[1] == App.Options.Help || args[1] == App.Options.HelpShort))
        {
            Help.ShowCommandHelp(command);
            return true;
        }

        return false;
    }

    private async Task ExecuteCommand(string command, string[] args)
    {
        if (command == "" || command == App.Commands.Watch)
        {
            var watchService = serviceProvider.GetRequiredService<WatchService>();
            await watchService.Watch(args);
            return;
        }
        
        await _workflowFactory.ExecuteAsync(command, args);
    }
}