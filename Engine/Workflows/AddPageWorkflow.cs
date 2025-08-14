using Engine.Extensions;
using Engine.Workers;
using Engine.Workers.Server;
using Engine.Workers.Shared;

namespace Engine.Workflows;

public class AddPageWorkflow(
    AppContext context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker) : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    public override string WorkflowName => Commands.AddPage;

    public override async Task ExecuteAsync(string[] args)
    {
        // AddPageWorkflow handles its own initialization due to complex arg parsing
        var filteredArgs = args.Where(arg => arg != WorkflowName).ToArray();
        string? pageName = null;

        if (filteredArgs.Length == 1)
        {
            // Single arg - auto-detect project, arg is page name
            InitializeWorkspace([]);
            pageName = filteredArgs[0];
        }
        else if (filteredArgs.Length == 2)
        {
            // Two args - project name and page name specified
            InitializeWorkspace([filteredArgs[0]]);
            pageName = filteredArgs[1];
        }
        else
        {
            throw new ArgumentException($"Usage: {App.Name} {Commands.AddPage} <page-name> or {App.Name} {Commands.AddPage} <project-name> <page-name>");
        }

        if (string.IsNullOrEmpty(pageName))
            throw new ArgumentException($"Page name is required");

        var pagePath = Context.ClientPagesPath.Combine(pageName);
        if (Directory.Exists(pagePath))
            throw new InvalidOperationException($"Page '{pageName}' already exists");

        pagePath.Create();

        await ExecuteWorkersAsync(async worker => await worker.AddPageAsync(pageName));
    }

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        // Not used - AddPageWorkflow overrides ExecuteAsync completely
        await Task.CompletedTask;
    }
}