using Engine.Extensions;
using Engine.Workers.Client;

namespace Engine.Workflows;

public class AddPageWorkflow(AppContext context) : BaseWorkflow(context)
{
    public override string WorkflowName => Commands.AddPage;

    public override async Task ExecuteAsync(string[] args)
    {
        var pageName = args.SingleOrDefault();
        if (string.IsNullOrEmpty(pageName))
            throw new ArgumentException($"Usage: {App.Name} {Commands.AddPage} <page-name>");

        var pagePath = Context.ClientPagesPath.Combine(pageName);
        if (Directory.Exists(pagePath))
            throw new InvalidOperationException($"Page '{pageName}' already exists");

        pagePath.Create();

        await ExecuteWorkersAsync(async worker =>
        {
            if (worker is IClientWorker clientWorker)
                await clientWorker.AddPage(pagePath);
        });
    }
}