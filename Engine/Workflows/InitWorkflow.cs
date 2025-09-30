using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Engine.Bridge;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Framework.Packaging;

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
        await ResourceHelpers.CopyEmbeddedRootFilesAsync(Resources.Path, Context.WorkingPath);
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.TypesPath, Context.WorkingPath.Combine(Folders.Types));
        TrimTypeScriptReferences(mode);

        bool preferRegistry = PackageSourceSelector.ShouldPreferRegistry();
        PackageWorkspaceAdapter workspaceAdapter = new(Context);
        await FrontendPackageInstaller.EnsureAsync(workspaceAdapter, preferRegistry);
        await TestPackageInstaller.EnsureAsync(workspaceAdapter, preferRegistry);
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

    private void TrimTypeScriptReferences(ProjectMode mode)
    {
        string tsConfigPath = Context.WorkingPath.Combine(Files.BaseTsConfigJson);
        if (!File.Exists(tsConfigPath))
        {
            return;
        }

        if (JsonNode.Parse(File.ReadAllText(tsConfigPath)) is not JsonObject root)
        {
            return;
        }

        JsonArray references = root["references"] as JsonArray ?? [];
        references.Clear();

        foreach (string path in GetTsReferences(mode))
        {
            references.Add(new JsonObject
            {
                ["path"] = path
            });
        }

        root["references"] = references;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        File.WriteAllText(tsConfigPath, root.ToJsonString(options) + Environment.NewLine);
    }

    private static IEnumerable<string> GetTsReferences(ProjectMode mode)
    {
        yield return Path.Combine(Folders.Src, Folders.Shared);

        if (mode is ProjectMode.Fullstack or ProjectMode.ClientOnly)
        {
            yield return Path.Combine(Folders.Src, Folders.Frontend);
        }

        if (mode is ProjectMode.Fullstack or ProjectMode.ServerOnly)
        {
            yield return Path.Combine(Folders.Src, Folders.Backend);
        }
    }
}
