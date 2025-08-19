using Engine.Extensions;
using Engine.Workers;

namespace Engine.Workflows;

public class AddPageWorkflow(
    AppWorkspace context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker) 
    : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    public override string WorkflowName => Commands.AddPage;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        var filteredArgs = args.Where(arg => arg != WorkflowName).ToArray();
        var nonFlagArgs = filteredArgs.Where(arg => !arg.StartsWith("--") && !arg.StartsWith('-')).ToArray();
        var pageName = nonFlagArgs.FirstOrDefault();

        if (string.IsNullOrEmpty(pageName))
            throw new ArgumentException($"Usage: {App.Name} {Commands.AddPage} <page-name> [--project-name <project>]");    

        var pagePath = Context.ClientPagesPath.Combine(pageName);
        if (Directory.Exists(pagePath))
            throw new InvalidOperationException($"Page '{pageName}' already exists");

        pagePath.Create();

        await ExecuteWorkersAsync(async worker => await worker.AddPageAsync(pageName));
    }
}