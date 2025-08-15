using Engine.Services;
using Microsoft.Extensions.DependencyInjection;
using Engine;

namespace CLI;

public class Runner(IServiceProvider serviceProvider)
{
    private Engine.AppContext _context = null!;
    private IWorkflowFactory _workflowFactory = null!;
        
    public async Task Run(string[] args)
    {
        var command = args.Length != 0
            ? args.First()
            : string.Empty;

        if (IsHelpRequested(command, args))
            return;

        // Runner doesn't interpret args - just passes them to workflows
        var workingPath = Directory.GetCurrentDirectory();
        var workflowArgs = args;

        using var scope = serviceProvider.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<Engine.AppContext>();
        _workflowFactory = scope.ServiceProvider.GetRequiredService<IWorkflowFactory>();
        _context.Initialize(workingPath);

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
        if (command == "" || command == Commands.Watch)
        {
            var watchService = serviceProvider.GetRequiredService<WatchService>();
            await watchService.Watch(args);
            return;
        }
        
        await _workflowFactory.ExecuteAsync(command, args);
    }
}