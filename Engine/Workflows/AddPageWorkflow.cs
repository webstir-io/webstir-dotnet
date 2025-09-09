using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Workers;

namespace Engine.Workflows;

public class AddPageWorkflow(
    AppWorkspace context,
    FrontendWorker clientWorker,
    BackendWorker serverWorker,
    SharedWorker sharedWorker)
    : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
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

        await ExecuteWorkersAsync(async worker => await worker.AddPageAsync(pageName));
    }
}
