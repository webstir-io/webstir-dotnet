using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;

namespace Engine.Workflows;

public sealed class RepairWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers)
    : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.Repair;

    protected override void InitializeWorkspace(string[] args)
    {
        string[] filteredArgs = [.. args.Where(arg => arg != WorkflowName)];
        string? workspaceOverride = ResolveWorkspaceOverride(filteredArgs);
        if (!string.IsNullOrWhiteSpace(workspaceOverride))
        {
            string fullPath = Path.GetFullPath(workspaceOverride, Context.WorkingPath);
            if (Directory.Exists(fullPath))
            {
                Context.Initialize(fullPath);
                filteredArgs = filteredArgs.Where(arg => arg != workspaceOverride).ToArray();
            }
        }

        string? projectName = GetProjectFromFlags(filteredArgs);
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            string projectPath = Context.WorkingPath.Combine(projectName);
            if (!Directory.Exists(projectPath))
            {
                throw new WorkflowUsageException($"Project directory '{projectName}' not found in current directory.");
            }

            Context.Initialize(projectPath);
            SetWorkspaceProfile(Context.DetectWorkspaceProfile());
            return;
        }

        string packageJsonPath = Context.WorkingPath.Combine(Files.PackageJson);
        if (File.Exists(packageJsonPath))
        {
            SetWorkspaceProfile(Context.DetectWorkspaceProfile());
            return;
        }

        base.InitializeWorkspace(args);
    }

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        bool dryRun = args.Any(arg => string.Equals(arg, RepairOptions.DryRun, StringComparison.Ordinal));

        string workspaceRoot = Context.WorkingPath;

        string modePrefix = WorkspaceProfile.Mode switch
        {
            WorkspaceMode.Ssg => $"{Resources.TemplatesPath}.{InitModes.Ssg}",
            WorkspaceMode.Spa => $"{Resources.TemplatesPath}.{InitModes.Spa}",
            WorkspaceMode.Api => $"{Resources.TemplatesPath}.{InitModes.Api}",
            _ => $"{Resources.TemplatesPath}.{InitModes.Full}"
        };

        List<string> missing = GetMissingFiles(workspaceRoot, modePrefix);

        if (dryRun)
        {
            PrintPlan(workspaceRoot, missing);
            return;
        }

        Directory.CreateDirectory(workspaceRoot);

        await ResourceHelpers.CopyEmbeddedRootFilesAsync(Resources.Path, workspaceRoot, overwriteExisting: false);
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.TypesPath, workspaceRoot.Combine(Folders.Types), overwriteExisting: false);
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(modePrefix, workspaceRoot, overwriteExisting: false);

        EnableFlags enable = ReadEnableFlags(workspaceRoot);
        if (WorkspaceProfile.HasFrontend)
        {
            await RepairEnabledFeaturesAsync(enable);
        }

        if (missing.Count > 0)
        {
            Console.WriteLine($"Restored {missing.Count} missing file(s).");
        }
        else
        {
            Console.WriteLine("Nothing to repair.");
        }

        Console.WriteLine($"Repaired workspace at {workspaceRoot} ({WorkspaceProfile.Mode}).");
    }

    private static string? ResolveWorkspaceOverride(string[] args)
    {
        for (int index = args.Length - 1; index >= 0; index--)
        {
            string candidate = args[index];
            if (candidate.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            if (!LooksLikeWorkspacePath(candidate))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static bool LooksLikeWorkspacePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Path.IsPathRooted(value) ||
            value.StartsWith(".", StringComparison.Ordinal) ||
            value.Contains(Path.DirectorySeparatorChar) ||
            value.Contains(Path.AltDirectorySeparatorChar);
    }

    private sealed record EnableFlags(bool Spa, bool ClientNav);

    private static void PrintPlan(string workspaceRoot, IReadOnlyCollection<string> missing)
    {
        if (missing.Count == 0)
        {
            Console.WriteLine("Nothing to repair.");
            return;
        }

        Console.WriteLine($"Would restore {missing.Count} missing file(s) in {workspaceRoot}:");
        foreach (string relativePath in missing.OrderBy(path => path, StringComparer.Ordinal))
        {
            Console.WriteLine($"  + {relativePath}");
        }
    }

    private List<string> GetMissingFiles(string workspaceRoot, string modePrefix)
    {
        HashSet<string> missing = new(StringComparer.Ordinal);

        foreach (string fileName in ResourceHelpers.ListEmbeddedRootFiles(Resources.Path))
        {
            string outputPath = Path.Combine(workspaceRoot, fileName);
            if (!File.Exists(outputPath))
            {
                missing.Add(fileName);
            }
        }

        foreach (string relativePath in ResourceHelpers.ListEmbeddedDirectoryFiles(Resources.TypesPath))
        {
            string outputPath = Path.Combine(workspaceRoot, Folders.Types, relativePath);
            if (!File.Exists(outputPath))
            {
                missing.Add(Path.Combine(Folders.Types, relativePath));
            }
        }

        foreach (string relativePath in ResourceHelpers.ListEmbeddedDirectoryFiles(modePrefix))
        {
            string outputPath = Path.Combine(workspaceRoot, relativePath);
            if (!File.Exists(outputPath))
            {
                missing.Add(relativePath);
            }
        }

        EnableFlags enable = ReadEnableFlags(workspaceRoot);
        if (WorkspaceProfile.HasFrontend)
        {
            string appDir = Path.Combine(Folders.Src, Folders.Frontend, Folders.App);

            if (enable.Spa)
            {
                foreach (string relativePath in ResourceHelpers.ListEmbeddedDirectoryFiles($"{Resources.FeaturesPath}.router"))
                {
                    string outputPath = Path.Combine(workspaceRoot, appDir, relativePath);
                    if (!File.Exists(outputPath))
                    {
                        missing.Add(Path.Combine(appDir, relativePath));
                    }
                }
            }

            if (enable.ClientNav)
            {
                string relativePath = Path.Combine(Folders.Scripts, "features", $"client-nav{FileExtensions.Ts}");
                string outputPath = Path.Combine(workspaceRoot, appDir, relativePath);
                if (!File.Exists(outputPath))
                {
                    missing.Add(Path.Combine(appDir, relativePath));
                }
            }
        }

        return [.. missing];
    }

    private static EnableFlags ReadEnableFlags(string workspaceRoot)
    {
        string packageJsonPath = workspaceRoot.Combine(Files.PackageJson);
        if (!File.Exists(packageJsonPath))
        {
            return new EnableFlags(Spa: false, ClientNav: false);
        }

        try
        {
            JsonNode? rootNode = JsonNode.Parse(File.ReadAllText(packageJsonPath));
            JsonObject? root = rootNode as JsonObject;
            JsonObject? webstir = root?["webstir"] as JsonObject;
            JsonObject? enable = webstir?["enable"] as JsonObject;

            bool spa = (bool?)enable?["spa"] ?? false;
            bool clientNav = (bool?)enable?["clientNav"] ?? false;

            return new EnableFlags(Spa: spa, ClientNav: clientNav);
        }
        catch
        {
            return new EnableFlags(Spa: false, ClientNav: false);
        }
    }

    private async Task RepairEnabledFeaturesAsync(EnableFlags enable)
    {
        string appDir = Context.FrontendAppPath;

        if (enable.Spa)
        {
            await ResourceHelpers.CopyEmbeddedDirectoryAsync($"{Resources.FeaturesPath}.router", appDir, overwriteExisting: false);
        }

        if (enable.ClientNav)
        {
            await ResourceHelpers.CopyEmbeddedFileAsync(
                $"{Resources.FeaturesPath}.client_nav.client_nav.ts",
                Path.Combine(appDir, Folders.Scripts, "features", $"client-nav{FileExtensions.Ts}"),
                overwriteExisting: false).ConfigureAwait(false);
        }
    }
}
