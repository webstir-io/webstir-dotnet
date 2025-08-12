using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;
using Engine.Modules;

namespace Engine.Workflows;

public class InitWorkflow(AppContext context, IEnumerable<IAppModule> modules) : BaseWorkflow(context, modules)
{
    public override string WorkflowName => Commands.Init;

    public override async Task ExecuteAsync(string[] args)
    {
        var mode = ParseProjectMode(args);

        // Mode must be spcecified in this spefic workflow because it initializes the project structure
        await ExecuteWorkersAsync(async worker => await worker.Init(mode), mode);
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