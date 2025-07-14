using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Engine.Services;
using Engine.Workflows;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Engine;
using Engine.Extensions;

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
        switch (command)
        {
            case App.Commands.Init:
                await _workflowFactory.ExecuteAsync(command, args[1..]);
                break;

            case App.Commands.AddPage:
                await _workflowFactory.ExecuteAsync(command, args[1..]);
                break;

            case App.Commands.Build:
                await _workflowFactory.ExecuteAsync(command, args);
                break;

            case App.Commands.Publish:
                await _workflowFactory.ExecuteAsync(command, args);
                break;

            case "":
            case App.Commands.Watch:
                await ExecuteWatchWorkflow(args);
                break;

            case App.Commands.Demo:
                await ExecuteDemoWorkflow(args[1..]);
                break;

            default:
                ShowUnknownCommandError(command);
                break;
        }
    }

    private static void ShowUnknownCommandError(string command)
    {
        Console.WriteLine($"Unknown command '{command}'");
        Console.WriteLine();
        Console.WriteLine($"Run '{App.Name} {App.Commands.Help}' to see available commands.");
    }


    private async Task ExecuteWatchWorkflow(string[] args)
    {
        var watcherService = serviceProvider.GetRequiredService<WatchService>();
        
        // Initial build
        await _workflowFactory.ExecuteAsync(App.Commands.Build, args);
        
        // Watch for changes
        await watcherService.Watch(async cleanBuild => 
        {
            var buildArgs = cleanBuild ? new[] { App.Options.Clean } : Array.Empty<string>();
            await _workflowFactory.ExecuteAsync(App.Commands.Build, buildArgs);
        });
    }

    private async Task ExecuteDemoWorkflow(string[] args)
    {
        // TODO: Implement demo workflow properly
        var targetDirectory = args.FirstOrDefault() ?? App.Folders.Demo;
        Console.WriteLine($"Demo functionality is temporarily disabled during refactoring.");
        Console.WriteLine($"Target directory would be: {targetDirectory}");
        await Task.CompletedTask;
    }
    
    private static ProjectMode ParseProjectMode(string[] args)
    {
        if (args.Contains(App.Options.ClientOnly)) return ProjectMode.ClientOnly;
        if (args.Contains(App.Options.ServerOnly)) return ProjectMode.ServerOnly;
        return ProjectMode.Fullstack;
    }
}