using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
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

    private enum InitMode
    {
        Full,
        Ssg,
        Spa,
        Api
    }

    protected override void InitializeWorkspace(string[] args)
    {
        // When running init in the current working directory, allow a custom project folder name.
        // Fallback to the default seed folder if no name is provided.
        if (Context.WorkingPath == Directory.GetCurrentDirectory())
        {
            InitArguments initArgs = ParseInitArguments(args);
            string targetFolder = !string.IsNullOrWhiteSpace(initArgs.ProjectName)
                ? initArgs.ProjectName!
                : Folders.Seed;

            Context.Initialize(Context.WorkingPath.Combine(targetFolder));
        }
    }

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        InitArguments initArgs = ParseInitArguments(args);
        WorkspaceProfile profile = MapToWorkspaceProfile(initArgs.Mode);
        SetWorkspaceProfile(profile);
        await ResourceHelpers.CopyEmbeddedRootFilesAsync(Resources.Path, Context.WorkingPath);
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.TypesPath, Context.WorkingPath.Combine(Folders.Types));
        await CopyModeTemplatesAsync(initArgs.Mode);

        await TrimFrameworkDependenciesAsync(Context.WorkingPath, profile);

        PackageWorkspaceAdapter workspaceAdapter = new(Context);
        if (profile.HasFrontend)
        {
            await FrontendPackageInstaller.EnsureAsync(workspaceAdapter);
        }

        await TestPackageInstaller.EnsureAsync(workspaceAdapter);

        if (profile.HasBackend)
        {
            await BackendPackageInstaller.EnsureAsync(workspaceAdapter);
        }

        await ExecuteWorkersAsync(async worker => await worker.InitAsync(profile), profile);
        await ApplyInitModeCustomizationsAsync(initArgs.Mode);
        TrimTypeScriptReferences(profile);
    }

    private readonly record struct InitArguments(InitMode Mode, string? ProjectName);

    private static InitArguments ParseInitArguments(string[] args)
    {
        string[] filteredArgs = [.. args.Where(arg => arg != Commands.Init)];

        // Support optional --project/-p <name>
        string? projectNameFromFlags = GetProjectFromFlags(filteredArgs);

        string? modeToken = null;
        string? positionalName = null;

        for (int index = 0; index < filteredArgs.Length; index++)
        {
            string arg = filteredArgs[index];

            // Skip project-name flags and their value; already handled via GetProjectFromFlags.
            if (arg is ProjectOptions.ProjectName or ProjectOptions.ProjectNameShort)
            {
                index++;
                continue;
            }

            if (IsInitModeToken(arg))
            {
                if (modeToken is null)
                {
                    modeToken = arg;
                }
                continue;
            }

            if (positionalName is null && !arg.StartsWith("-", StringComparison.Ordinal))
            {
                positionalName = arg;
            }
        }

        InitMode mode = ParseInitMode(modeToken);
        string? projectName = projectNameFromFlags ?? positionalName;
        return new InitArguments(mode, projectName);
    }

    private static bool IsInitModeToken(string value)
    {
        string token = value.ToLowerInvariant();
        return token is InitModes.Full or InitModes.Ssg or InitModes.Spa or InitModes.Api;
    }

    private static InitMode ParseInitMode(string? token)
    {
        return token?.ToLowerInvariant() switch
        {
            InitModes.Ssg => InitMode.Ssg,
            InitModes.Spa => InitMode.Spa,
            InitModes.Api => InitMode.Api,
            InitModes.Full => InitMode.Full,
            _ => InitMode.Full
        };
    }

    private static WorkspaceProfile MapToWorkspaceProfile(InitMode mode) =>
        mode switch
        {
            InitMode.Ssg => WorkspaceProfile.Ssg,
            InitMode.Spa => WorkspaceProfile.Spa,
            InitMode.Api => WorkspaceProfile.Api,
            _ => WorkspaceProfile.Full
        };

    private static async Task TrimFrameworkDependenciesAsync(string workspaceRoot, WorkspaceProfile profile)
    {
        string packageJsonPath = workspaceRoot.Combine(Files.PackageJson);
        if (!File.Exists(packageJsonPath))
        {
            return;
        }

        JsonNode? rootNode = JsonNode.Parse(await File.ReadAllTextAsync(packageJsonPath));
        if (rootNode is not JsonObject root)
        {
            return;
        }

        if (root["dependencies"] is not JsonObject dependencies)
        {
            return;
        }

        if (!profile.HasFrontend)
        {
            dependencies.Remove(FrameworkPackageCatalog.Frontend.Name);
        }

        if (!profile.HasBackend)
        {
            dependencies.Remove(FrameworkPackageCatalog.Backend.Name);
        }

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(packageJsonPath, root.ToJsonString(options) + Environment.NewLine);
    }

    private async Task ApplyInitModeCustomizationsAsync(InitMode mode)
    {
        switch (mode)
        {
            case InitMode.Ssg:
                await ApplySsgCustomizationsAsync();
                break;
            case InitMode.Spa:
                await ApplySpaCustomizationsAsync();
                break;
            case InitMode.Api:
                await ApplyApiCustomizationsAsync();
                break;
            case InitMode.Full:
                await ApplyFullCustomizationsAsync();
                break;
        }
    }

    private async Task CopyModeTemplatesAsync(InitMode mode)
    {
        string? resourcePrefix = mode switch
        {
            InitMode.Ssg => $"{Resources.TemplatesPath}.ssg",
            InitMode.Spa => $"{Resources.TemplatesPath}.spa",
            InitMode.Api => $"{Resources.TemplatesPath}.api",
            InitMode.Full => $"{Resources.TemplatesPath}.full",
            _ => null
        };

        if (resourcePrefix is null)
        {
            return;
        }

        await ResourceHelpers.CopyEmbeddedDirectoryAsync(resourcePrefix, Context.WorkingPath);
    }

    private async Task ApplySsgCustomizationsAsync()
    {
        string workspaceRoot = Context.WorkingPath;
        await CustomizePackageJsonAsync(workspaceRoot);
        await CustomizeHomePage(workspaceRoot);
        await CustomizeHomeTest(workspaceRoot);
        RemoveSpaRoutingFiles(workspaceRoot);
        RemoveSharedFolder(workspaceRoot);
    }

    private async Task ApplySpaCustomizationsAsync()
    {
        string workspaceRoot = Context.WorkingPath;
        await UpdatePackageJsonAsync(workspaceRoot, "SPA frontend workspace for Webstir.", "spa", null);
    }

    private async Task ApplyApiCustomizationsAsync()
    {
        string workspaceRoot = Context.WorkingPath;
        await UpdatePackageJsonAsync(workspaceRoot, "Backend API workspace for Webstir.", "api", null);

        string frontendPath = workspaceRoot
            .Combine(Folders.Src)
            .Combine(Folders.Frontend);

        if (Directory.Exists(frontendPath))
        {
            Directory.Delete(frontendPath, recursive: true);
        }
    }

    private async Task ApplyFullCustomizationsAsync()
    {
        string workspaceRoot = Context.WorkingPath;
        await UpdatePackageJsonAsync(workspaceRoot, null, "full", null);
    }

    private static async Task CustomizePackageJsonAsync(string workspaceRoot)
    {
        await UpdatePackageJsonAsync(
            workspaceRoot,
            "Static site (SSG) workspace for Webstir.",
            "ssg",
            null);
    }

    private static async Task UpdatePackageJsonAsync(
        string workspaceRoot,
        string? description,
        string mode,
        Action<JsonObject>? mutateModuleManifest)
    {
        string packageJsonPath = workspaceRoot.Combine(Files.PackageJson);
        if (!File.Exists(packageJsonPath))
        {
            return;
        }

        JsonNode? rootNode = JsonNode.Parse(await File.ReadAllTextAsync(packageJsonPath));
        if (rootNode is not JsonObject root)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(description) && root["description"] is JsonValue)
        {
            root["description"] = description;
        }

        JsonObject webstir = root["webstir"] as JsonObject ?? new JsonObject();
        webstir["mode"] = mode;

        JsonObject moduleManifest = webstir["moduleManifest"] as JsonObject ?? new JsonObject();
        mutateModuleManifest?.Invoke(moduleManifest);
        webstir["moduleManifest"] = moduleManifest;
        root["webstir"] = webstir;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        await File.WriteAllTextAsync(packageJsonPath, root.ToJsonString(options) + Environment.NewLine);
    }

    private static async Task<string> ReadEmbeddedTextAsync(string resourceName)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        }

        using StreamReader reader = new(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task CustomizeHomePage(string workspaceRoot)
    {
        string homeHtmlPath = workspaceRoot
            .Combine(Folders.Src)
            .Combine(Folders.Frontend)
            .Combine(Folders.Pages)
            .Combine(Folders.Home)
            .Combine(Files.IndexHtml);

        if (!File.Exists(homeHtmlPath))
        {
            return;
        }

        string resourceName = $"{Resources.TemplatesPath}.ssg.src.frontend.pages.home.index.html";
        string content = await ReadEmbeddedTextAsync(resourceName);
        File.WriteAllText(homeHtmlPath, content);
    }

    private static async Task CustomizeHomeTest(string workspaceRoot)
    {
        string homeTestPath = workspaceRoot
            .Combine(Folders.Src)
            .Combine(Folders.Frontend)
            .Combine(Folders.Pages)
            .Combine(Folders.Home)
            .Combine(Folders.Tests)
            .Combine("home.test.ts");

        if (!File.Exists(homeTestPath))
        {
            return;
        }

        string resourceName = $"{Resources.TemplatesPath}.ssg.src.frontend.pages.home.tests.home.test.ts";
        string content = await ReadEmbeddedTextAsync(resourceName);
        File.WriteAllText(homeTestPath, content);
    }

    private static void RemoveSpaRoutingFiles(string workspaceRoot)
    {
        string frontendAppPath = workspaceRoot
            .Combine(Folders.Src)
            .Combine(Folders.Frontend)
            .Combine(Folders.App);

        string sharedPath = workspaceRoot
            .Combine(Folders.Src)
            .Combine(Folders.Shared);

        string[] filesToDelete =
        [
            frontendAppPath.Combine("navigation.ts"),
            frontendAppPath.Combine("router.ts"),
            sharedPath.Combine("router-types.ts"),
            workspaceRoot.Combine(Folders.Src).Combine(Folders.Frontend).Combine(Folders.Pages).Combine(Folders.Home).Combine("index.ts")
        ];

        foreach (string file in filesToDelete)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    private static void RemoveSharedFolder(string workspaceRoot)
    {
        string sharedPath = workspaceRoot
            .Combine(Folders.Src)
            .Combine(Folders.Shared);

        if (Directory.Exists(sharedPath))
        {
            Directory.Delete(sharedPath, recursive: true);
        }
    }
    private void TrimTypeScriptReferences(WorkspaceProfile profile)
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

        foreach (string path in GetTsReferences(profile, Context.WorkingPath))
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

    private static IEnumerable<string> GetTsReferences(WorkspaceProfile profile, string workspaceRoot)
    {
        string sharedPath = Path.Combine(workspaceRoot, Folders.Src, Folders.Shared);
        if (Directory.Exists(sharedPath))
        {
            yield return Path.Combine(Folders.Src, Folders.Shared);
        }

        if (profile.HasFrontend)
        {
            yield return Path.Combine(Folders.Src, Folders.Frontend);
        }

        if (profile.HasBackend)
        {
            yield return Path.Combine(Folders.Src, Folders.Backend);
        }
    }
}
