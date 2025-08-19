using Engine;
using Engine.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CLI;

public class Runner(IServiceProvider serviceProvider)
{
    private AppWorkspace _workspace = null!;
    private IWorkflowFactory _workflowFactory = null!;
        
    public async Task Run(string[] args)
    {
        var command = args.Length != 0
            ? args.First()
            : string.Empty;

        if (IsHelpRequested(command, args))
            return;

        var workingPath = Directory.GetCurrentDirectory();
        var workflowArgs = args;

        using var scope = serviceProvider.CreateScope();
        _workspace = scope.ServiceProvider.GetRequiredService<Engine.AppWorkspace>();
        _workflowFactory = scope.ServiceProvider.GetRequiredService<IWorkflowFactory>();
        _workspace.Initialize(workingPath);

        await ExecuteCommand(command, workflowArgs);
    }


    private static bool IsHelpRequested(string command, string[] args)
    {
        if (command == Commands.Help || command == HelpOptions.Help || command == HelpOptions.HelpShort)
        {
            if (args.Length > 1 && command == Commands.Help)
                Help.ShowCommandHelp(args[1]);
            else
                Help.ShowGeneralHelp();
            return true;
        }

        if (args.Length > 1 && (args[1] == HelpOptions.Help || args[1] == HelpOptions.HelpShort))
        {
            Help.ShowCommandHelp(command);
            return true;
        }

        return false;
    }

    private async Task ExecuteCommand(string command, string[] args)
    {
        if (command == "")
            command = Commands.Watch;
        
        await _workflowFactory.ExecuteAsync(command, args);
    }
}