using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using System.Collections.Generic;
using Engine.Workflows.Interfaces;

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
            throw new ArgumentException($"Usage: {App.Name} {Commands.AddPage} <page-name> [--project-name <project>]");
        }

        string pagePath = Context.FrontendPagesPath.Combine(pageName);
        if (Directory.Exists(pagePath))
        {
            throw new InvalidOperationException($"Page '{pageName}' already exists");
        }

        pagePath.Create();

        // Only the frontend worker participates in adding pages
        await Frontend.AddPageAsync(pageName);
    }
}
