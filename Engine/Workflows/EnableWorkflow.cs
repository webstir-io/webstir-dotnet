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
        ClientNav,
        Search,
        Backend
    }

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string[] filteredArgs = [.. args.Where(arg => arg != WorkflowName)];
        if (filteredArgs.Length == 0)
        {
            throw new WorkflowUsageException($"Usage: {App.Name} {Commands.Enable} <scripts <page>|spa|client-nav|search|backend>");
        }

        Feature feature = ParseFeature(filteredArgs[0]);

        switch (feature)
        {
            case Feature.Scripts:
                string? page = filteredArgs.Skip(1).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(page))
                {
                    throw new WorkflowUsageException($"Usage: {App.Name} {Commands.Enable} scripts <page>");
                }
                await EnableScriptsAsync(page);
                break;
            case Feature.Spa:
                await EnableSpaAsync();
                break;
            case Feature.ClientNav:
                await EnableClientNavAsync();
                break;
            case Feature.Search:
                await EnableSearchAsync();
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
            "client-nav" => Feature.ClientNav,
            "search" => Feature.Search,
            "backend" => Feature.Backend,
            _ => throw new WorkflowUsageException(
                $"Unknown feature '{token}'. Expected scripts, spa, client-nav, search, or backend. " +
                $"Usage: {App.Name} {Commands.Enable} <scripts <page>|spa|client-nav|search|backend>")
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

        string templatePrefix = $"{Resources.FeaturesPath}.page_script";
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(templatePrefix, pageDir);

        string relativePath = Path.Combine(Folders.Src, Folders.Frontend, Folders.Pages, pageName, $"index{FileExtensions.Ts}");
        Console.WriteLine($"Enabled scripts for page '{pageName}'.");
        Console.WriteLine($"  + {relativePath}");
    }

    private async Task EnableSpaAsync()
    {
        string appDir = Context.FrontendAppPath;
        await ResourceHelpers.CopyEmbeddedDirectoryAsync($"{Resources.FeaturesPath}.router", appDir);
        bool updatedPackageJson = await UpdatePackageJsonAsync(enableSpa: true, enableClientNav: null, enableSearch: null, enableBackend: null, mode: null);

        Console.WriteLine("Enabled spa.");
        if (updatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.enable.spa=true");
        }
    }

    private async Task EnableClientNavAsync()
    {
        string appDir = Context.FrontendAppPath;
        await ResourceHelpers.CopyEmbeddedDirectoryAsync($"{Resources.FeaturesPath}.client_nav", appDir);
        bool updatedPackageJson = await UpdatePackageJsonAsync(enableSpa: null, enableClientNav: true, enableSearch: null, enableBackend: null, mode: null);

        string relativePath = Path.Combine(Folders.Src, Folders.Frontend, Folders.App, $"clientNav{FileExtensions.Js}");
        Console.WriteLine("Enabled client-nav.");
        Console.WriteLine($"  + {relativePath}");
        if (updatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.enable.clientNav=true");
        }
    }

    private async Task EnableSearchAsync()
    {
        string appDir = Context.FrontendAppPath;
        await ResourceHelpers.CopyEmbeddedDirectoryAsync($"{Resources.FeaturesPath}.search", appDir);
        bool updatedPackageJson = await UpdatePackageJsonAsync(enableSpa: null, enableClientNav: null, enableSearch: true, enableBackend: null, mode: null);

        string relativePath = Path.Combine(Folders.Src, Folders.Frontend, Folders.App, $"search{FileExtensions.Js}");
        Console.WriteLine("Enabled search.");
        Console.WriteLine($"  + {relativePath}");
        if (updatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.enable.search=true");
        }
    }

    private async Task EnableBackendAsync()
    {
        string backendDir = Context.WorkingPath.Combine(Folders.Src).Combine(Folders.Backend);
        if (!Directory.Exists(backendDir))
        {
            string templatePrefix = $"{Resources.TemplatesPath}.full.{Folders.Src}.{Folders.Backend}";
            await ResourceHelpers.CopyEmbeddedDirectoryAsync(templatePrefix, backendDir);
        }

        bool updatedPackageJson = await UpdatePackageJsonAsync(enableSpa: null, enableClientNav: null, enableSearch: null, enableBackend: true, mode: "full");
        EnsureTsReference(Folders.Backend);

        Console.WriteLine("Enabled backend.");
        if (updatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.mode=full, webstir.enable.backend=true");
        }
    }

    private async Task<bool> UpdatePackageJsonAsync(bool? enableSpa, bool? enableClientNav, bool? enableSearch, bool? enableBackend, string? mode)
    {
        string packageJsonPath = Context.WorkingPath.Combine(Files.PackageJson);
        if (!File.Exists(packageJsonPath))
        {
            return false;
        }

        JsonNode? rootNode = JsonNode.Parse(await File.ReadAllTextAsync(packageJsonPath));
        if (rootNode is not JsonObject root)
        {
            return false;
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
        if (enableClientNav.HasValue)
        {
            enable["clientNav"] = enableClientNav.Value;
        }
        if (enableSearch.HasValue)
        {
            enable["search"] = enableSearch.Value;
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
        return true;
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
