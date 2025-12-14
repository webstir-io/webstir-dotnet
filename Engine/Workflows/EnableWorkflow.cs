using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;

namespace Engine.Workflows;

public class EnableWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers) : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.Enable;

    private enum Feature
    {
        Scripts,
        Spa,
        SeamlessNav,
        Backend
    }

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string[] filteredArgs = [.. args.Where(arg => arg != WorkflowName)];
        if (filteredArgs.Length == 0)
        {
            throw new ArgumentException($"Usage: {App.Name} {Commands.Enable} <scripts <page>|spa|seamless-nav|backend>");
        }

        Feature feature = ParseFeature(filteredArgs[0]);

        switch (feature)
        {
            case Feature.Scripts:
                string? page = filteredArgs.Skip(1).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(page))
                {
                    throw new ArgumentException($"Usage: {App.Name} {Commands.Enable} scripts <page>");
                }
                await EnableScriptsAsync(page);
                break;
            case Feature.Spa:
                await EnableSpaAsync();
                break;
            case Feature.SeamlessNav:
                await EnableSeamlessNavAsync();
                break;
            case Feature.Backend:
                await EnableBackendAsync();
                break;
        }
    }

    private static Feature ParseFeature(string token)
    {
        string normalized = token.ToLowerInvariant();
        return normalized switch
        {
            "scripts" => Feature.Scripts,
            "spa" => Feature.Spa,
            "seamless-nav" => Feature.SeamlessNav,
            "seamless" => Feature.SeamlessNav,
            "backend" => Feature.Backend,
            _ => throw new ArgumentException($"Unknown feature '{token}'. Expected scripts, spa, seamless-nav, or backend.")
        };
    }

    private async Task EnableScriptsAsync(string pageName)
    {
        string pageDir = Context.FrontendPagesPath.Combine(pageName);
        if (!Directory.Exists(pageDir))
        {
            throw new InvalidOperationException($"Page '{pageName}' does not exist. Create it first.");
        }

        string targetScript = Path.Combine(pageDir, "index.ts");
        if (File.Exists(targetScript))
        {
            throw new InvalidOperationException($"Page '{pageName}' already has an index.ts script.");
        }

        string templatePrefix = $"{Resources.FeaturesPath}.page-script";
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(templatePrefix, pageDir);
    }

    private async Task EnableSpaAsync()
    {
        string appDir = Context.FrontendAppPath;
        await ResourceHelpers.CopyEmbeddedDirectoryAsync($"{Resources.FeaturesPath}.router", appDir);
        await UpdatePackageJsonAsync(enableSpa: true, enableSeamlessNav: null, enableBackend: null, mode: null);
    }

    private async Task EnableSeamlessNavAsync()
    {
        string appDir = Context.FrontendAppPath;
        await ResourceHelpers.CopyEmbeddedDirectoryAsync($"{Resources.FeaturesPath}.seamless-nav", appDir);
        await UpdatePackageJsonAsync(enableSpa: null, enableSeamlessNav: true, enableBackend: null, mode: null);
    }

    private async Task EnableBackendAsync()
    {
        string backendDir = Context.WorkingPath.Combine(Folders.Src).Combine(Folders.Backend);
        if (!Directory.Exists(backendDir))
        {
            string templatePrefix = $"{Resources.TemplatesPath}.full.{Folders.Src}.{Folders.Backend}";
            await ResourceHelpers.CopyEmbeddedDirectoryAsync(templatePrefix, backendDir);
        }

        await UpdatePackageJsonAsync(enableSpa: null, enableSeamlessNav: null, enableBackend: true, mode: "full");
        EnsureTsReference(Folders.Backend);
    }

    private async Task UpdatePackageJsonAsync(bool? enableSpa, bool? enableSeamlessNav, bool? enableBackend, string? mode)
    {
        string packageJsonPath = Context.WorkingPath.Combine(Files.PackageJson);
        if (!File.Exists(packageJsonPath))
        {
            return;
        }

        JsonNode? rootNode = JsonNode.Parse(await File.ReadAllTextAsync(packageJsonPath));
        if (rootNode is not JsonObject root)
        {
            return;
        }

        JsonObject webstir = root["webstir"] as JsonObject ?? new JsonObject();
        if (!string.IsNullOrWhiteSpace(mode))
        {
            webstir["mode"] = mode;
        }

        JsonObject enable = webstir["enable"] as JsonObject ?? new JsonObject();
        if (enableSpa.HasValue)
        {
            enable["spa"] = enableSpa.Value;
        }
        if (enableSeamlessNav.HasValue)
        {
            enable["seamlessNav"] = enableSeamlessNav.Value;
        }
        if (enableBackend.HasValue)
        {
            enable["backend"] = enableBackend.Value;
        }

        webstir["enable"] = enable;
        root["webstir"] = webstir;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };
        await File.WriteAllTextAsync(packageJsonPath, root.ToJsonString(options) + Environment.NewLine);
    }

    private void EnsureTsReference(string folderName)
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

        string relativePath = Path.Combine(Folders.Src, folderName);
        bool exists = references.OfType<JsonObject>().Any(obj => string.Equals((string?)obj["path"], relativePath, StringComparison.Ordinal));
        if (!exists)
        {
            references.Add(new JsonObject
            {
                ["path"] = relativePath
            });
        }

        root["references"] = references;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };
        File.WriteAllText(tsConfigPath, root.ToJsonString(options) + Environment.NewLine);
    }
}
