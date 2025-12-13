using System;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using System.Collections.Generic;
using Engine.Interfaces;

namespace Engine.Workflows;

public class AddPageWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers)
    : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.AddPage;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string[] filteredArgs = [.. args.Where(arg => arg != WorkflowName)];
        string[] nonFlagArgs = [.. filteredArgs
            .Where(arg => !arg.StartsWith("--", StringComparison.Ordinal) && !arg.StartsWith('-'))
        ];
        string? pageName = nonFlagArgs.FirstOrDefault();

        if (string.IsNullOrEmpty(pageName))
        {
            throw new ArgumentException($"Usage: {App.Name} {Commands.AddPage} <page-name> [--project <project>]");
        }

        string pagePath = Context.FrontendPagesPath.Combine(pageName);
        if (pagePath.Exists())
        {
            throw new InvalidOperationException($"Page '{pageName}' already exists");
        }

        await Frontend.AddPageAsync(pageName);
    }
}
