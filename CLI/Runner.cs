using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine;
using Engine.Services;
using Engine.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace CLI;

public class Runner(IServiceProvider serviceProvider)
{
    private AppWorkspace _workspace = null!;
    private IWorkflowFactory _workflowFactory = null!;

    public async Task Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string command = args.Length != 0
            ? args.First()
            : string.Empty;

        if (IsHelpRequested(command, args))
        {
            return;
        }

        string workingPath = Directory.GetCurrentDirectory();
        string[] workflowArgs = args;

        using IServiceScope scope = serviceProvider.CreateScope();
        _workspace = scope.ServiceProvider.GetRequiredService<AppWorkspace>();
        _workflowFactory = scope.ServiceProvider.GetRequiredService<IWorkflowFactory>();
        _workspace.Initialize(workingPath);

        await ExecuteCommand(command, workflowArgs);
    }


    private static bool IsHelpRequested(string command, string[] args)
    {
        if (command is Commands.Help or HelpOptions.Help or HelpOptions.HelpShort)
        {
            if (args.Length > 1 && command == Commands.Help)
                Help.ShowCommandHelp(args[1]);
            else
                Help.ShowGeneralHelp();
            return true;
        }

        if (args.Length > 1 && args[1] is HelpOptions.Help or HelpOptions.HelpShort)
        {
            Help.ShowCommandHelp(command);
            return true;
        }

        return false;
    }

    private async Task ExecuteCommand(string command, string[] args)
    {
        if (string.IsNullOrEmpty(command))
        {
            command = Commands.Watch;
        }

        try
        {
            await _workflowFactory.ExecuteAsync(command, args);
        }
        catch (WorkflowUsageException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Environment.ExitCode = 1;
        }
    }
}
