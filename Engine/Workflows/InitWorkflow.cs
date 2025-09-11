using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Models;
using System.Collections.Generic;
using Engine.Interfaces;

namespace Engine.Workflows;

public class InitWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers) : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.Init;

    protected override void InitializeWorkspace(string[] args)
    {
        // When running init in the current working directory, allow a custom project folder name.
        // Fallback to the default seed folder if no name is provided.
        if (Context.WorkingPath == Directory.GetCurrentDirectory())
        {
            string[] filteredArgs = [.. args.Where(arg => arg != WorkflowName)];
            string? projectName = GetProjectFromFlags(filteredArgs);
            // Also support a positional directory argument: `webstir init my-app`
            if (string.IsNullOrWhiteSpace(projectName))
            {
                // Take the first non-option arg as a directory name
                projectName = filteredArgs.FirstOrDefault(arg => !arg.StartsWith('-'));
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
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Templates.TypesPath, Context.WorkingPath.Combine(Folders.Types));
        await ExecuteWorkersAsync(async worker => await worker.InitAsync(mode), mode);
    }

    private static ProjectMode ParseProjectMode(string[] args)
    {
        if (args.Contains(InitOptions.ClientOnly))
        {
            return ProjectMode.ClientOnly;
        }
        if (args.Contains(InitOptions.ServerOnly))
        {
            return ProjectMode.ServerOnly;
        }
        return ProjectMode.Fullstack;
    }
}
