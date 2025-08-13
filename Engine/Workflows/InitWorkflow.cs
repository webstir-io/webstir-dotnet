using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;
using Engine.Workers;
using Engine.Workers.Server;
using Engine.Workers.Shared;

namespace Engine.Workflows;

public class InitWorkflow(
    AppContext context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker) : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    public override string WorkflowName => Commands.Init;

    public override async Task ExecuteAsync(string[] args)
    {
        if (Context.WorkingPath == Directory.GetCurrentDirectory())
            Context.Initialize(Context.WorkingPath.Combine(Folders.Seed));

        var mode = ParseProjectMode(args);
        
        // Copy all embedded resources first
        ResourceHelpers.CopyEmbeddedDirectory("src", Context.WorkingPath);
        
        await ExecuteWorkersAsync(async worker => await worker.InitAsync(mode), mode);
        await CreatePackageJson();
    }

    private async Task CreatePackageJson()
    {
        var packageJsonPath = Context.WorkingPath.Combine(Files.PackageJson);        
        if (!File.Exists(packageJsonPath))
            await Task.Run(() => AssemblyHelpers.WriteResourceToFile(Files.PackageJson, packageJsonPath));
    }

    private static ProjectMode ParseProjectMode(string[] args)
    {
        if (args.Contains(InitOptions.ClientOnly)) return ProjectMode.ClientOnly;
        if (args.Contains(InitOptions.ServerOnly)) return ProjectMode.ServerOnly;
        return ProjectMode.Fullstack;
    }
}