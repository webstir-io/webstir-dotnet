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
        // When running init in the current working directory, allow a custom project folder name.
        // Fallback to the default seed folder if no name is provided.
        if (Context.WorkingPath == Directory.GetCurrentDirectory())
        {
            string[] filteredArgs = [.. args.Where(a => a != WorkflowName)];
            string? projectName = GetProjectFromFlags(filteredArgs);
            // Also support a positional directory argument: `webstir init my-app`
            if (string.IsNullOrWhiteSpace(projectName))
            {
                // Take the first non-option arg as a directory name
                projectName = filteredArgs.FirstOrDefault(a => !a.StartsWith('-'));
            }

            string targetFolder = !string.IsNullOrWhiteSpace(projectName)
                ? projectName!
                : Folders.Seed;

            Context.Initialize(Context.WorkingPath.Combine(targetFolder));
        }
    }

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        ProjectMode mode = ParseProjectMode(args);
        await ResourceHelpers.CopyEmbeddedRootFilesAsync(Templates.Path, Context.WorkingPath);
        await ExecuteWorkersAsync(async worker => await worker.InitAsync(mode), mode);
    }

    private static ProjectMode ParseProjectMode(string[] args)
    {
        if (args.Contains(InitOptions.ClientOnly))
            return ProjectMode.ClientOnly;
        if (args.Contains(InitOptions.ServerOnly))
            return ProjectMode.ServerOnly;
        return ProjectMode.Fullstack;
    }
}
