using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;
using Engine.Workers;

namespace Engine.Workflows;

public class InitWorkflow(
    AppWorkspace context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker) : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    public override string WorkflowName => Commands.Init;

    protected override void InitializeWorkspace(string[] args)
    {
        if (Context.WorkingPath == Directory.GetCurrentDirectory())
            Context.Initialize(Context.WorkingPath.Combine(Folders.Seed));
    }

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        var mode = ParseProjectMode(args);
        await ResourceHelpers.CopyEmbeddedRootFilesAsync(Resources.ResourcesPath, Context.WorkingPath);     
        await ExecuteWorkersAsync(async worker => await worker.InitAsync(mode), mode);
    }

    private static ProjectMode ParseProjectMode(string[] args)
    {
        if (args.Contains(InitOptions.ClientOnly)) return ProjectMode.ClientOnly;
        if (args.Contains(InitOptions.ServerOnly)) return ProjectMode.ServerOnly;
        return ProjectMode.Fullstack;
    }
}