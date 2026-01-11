using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
        ContentNav,
        Backend,
        GithubPages,
        GithubDeploy
    }

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string[] filteredArgs = [.. args.Where(arg => arg != WorkflowName)];
        if (filteredArgs.Length == 0)
        {
            throw new WorkflowUsageException(
                $"Usage: {App.Name} {Commands.Enable} <scripts <page>|spa|client-nav|search|content-nav|backend|github-pages|gh-deploy>");
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
            case Feature.ContentNav:
                await EnableContentNavAsync();
                break;
            case Feature.Backend:
                await EnableBackendAsync();
                break;
            case Feature.GithubPages:
                string? basePath = ResolveGithubPagesBasePathArgument(filteredArgs);
                await EnableGithubPagesAsync(basePath);
                break;
            case Feature.GithubDeploy:
                string? deploymentsBasePath = ResolveGithubPagesBasePathArgument(filteredArgs);
                await EnableGithubDeploymentsAsync(deploymentsBasePath);
                break;
        }
    }

    private static string? ResolveGithubPagesBasePathArgument(string[] filteredArgs)
    {
        ArgumentNullException.ThrowIfNull(filteredArgs);

        string? basePath = filteredArgs.Length >= 2
            ? filteredArgs[1]
            : null;

        if (filteredArgs.Length == 2 && LooksLikeExistingWorkspacePath(basePath))
        {
            return null;
        }

        return basePath;
    }

    private static bool LooksLikeExistingWorkspacePath(string? token)
    {
        if (!LooksLikeWorkspacePath(token))
        {
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(token!, Directory.GetCurrentDirectory());
            return Directory.Exists(fullPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool LooksLikeWorkspacePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Path.IsPathRooted(value)
            || value.StartsWith(".", StringComparison.Ordinal)
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar);
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
            "content-nav" => Feature.ContentNav,
            "backend" => Feature.Backend,
            "github-pages" => Feature.GithubPages,
            "gh-pages" => Feature.GithubPages,
            "gh-deploy" => Feature.GithubDeploy,
            _ => throw new WorkflowUsageException(
                $"Unknown feature '{token}'. Expected scripts, spa, client-nav, search, content-nav, backend, github-pages, or gh-deploy. " +
                $"Usage: {App.Name} {Commands.Enable} <scripts <page>|spa|client-nav|search|content-nav|backend|github-pages|gh-deploy>")
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
        bool updatedPackageJson = await UpdatePackageJsonAsync(
            enableSpa: true,
            enableClientNav: null,
            enableSearch: null,
            enableContentNav: null,
            enableBackend: null,
            enableGithubPages: null,
            mode: null);

        Console.WriteLine("Enabled spa.");
        if (updatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.enable.spa=true");
        }
    }

    private async Task EnableClientNavAsync()
    {
        string appDir = Context.FrontendAppPath;
        await ResourceHelpers.CopyEmbeddedFileAsync(
            $"{Resources.FeaturesPath}.client_nav.client_nav.ts",
            Path.Combine(appDir, Folders.Scripts, "features", $"client-nav{FileExtensions.Ts}")).ConfigureAwait(false);
        await EnableClientNavScriptAsync(appDir).ConfigureAwait(false);
        bool updatedPackageJson = await UpdatePackageJsonAsync(
            enableSpa: null,
            enableClientNav: true,
            enableSearch: null,
            enableContentNav: null,
            enableBackend: null,
            enableGithubPages: null,
            mode: null);

        string relativePath = Path.Combine(Folders.Src, Folders.Frontend, Folders.App, Folders.Scripts, "features", $"client-nav{FileExtensions.Ts}");
        Console.WriteLine("Enabled client-nav.");
        Console.WriteLine($"  + {relativePath}");
        if (updatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.enable.clientNav=true");
        }
    }

    private static async Task EnableClientNavScriptAsync(string appDir)
    {
        string appTsPath = Path.Combine(appDir, Files.AppTs);
        if (!File.Exists(appTsPath))
        {
            return;
        }

        string source = await File.ReadAllTextAsync(appTsPath).ConfigureAwait(false);
        string updated = EnsureClientNavScriptImport(source);
        if (!string.Equals(source, updated, StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(appTsPath, updated).ConfigureAwait(false);
        }
    }

    private async Task EnableSearchAsync()
    {
        string appDir = Context.FrontendAppPath;
        await ResourceHelpers.CopyEmbeddedFileAsync(
            $"{Resources.FeaturesPath}.search.search.ts",
            Path.Combine(appDir, Folders.Scripts, "features", $"search{FileExtensions.Ts}")).ConfigureAwait(false);
        await ResourceHelpers.CopyEmbeddedFileAsync(
            $"{Resources.FeaturesPath}.search.search.css",
            Path.Combine(appDir, Folders.Styles, "features", $"search{FileExtensions.Css}")).ConfigureAwait(false);
        await EnableSearchCssAsync(appDir).ConfigureAwait(false);
        await EnsureSearchCssModeEnabledAsync(appDir).ConfigureAwait(false);
        await EnableSearchScriptAsync(appDir).ConfigureAwait(false);
        bool updatedPackageJson = await UpdatePackageJsonAsync(
            enableSpa: null,
            enableClientNav: null,
            enableSearch: true,
            enableContentNav: null,
            enableBackend: null,
            enableGithubPages: null,
            mode: null);

        string relativePath = Path.Combine(Folders.Src, Folders.Frontend, Folders.App, Folders.Scripts, "features", $"search{FileExtensions.Ts}");
        Console.WriteLine("Enabled search.");
        Console.WriteLine($"  + {relativePath}");
        if (updatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.enable.search=true");
        }
    }

    private async Task EnableContentNavAsync()
    {
        string appDir = Context.FrontendAppPath;
        await ResourceHelpers.CopyEmbeddedFileAsync(
            $"{Resources.FeaturesPath}.content_nav.content_nav.ts",
            Path.Combine(appDir, Folders.Scripts, "features", $"content-nav{FileExtensions.Ts}")).ConfigureAwait(false);
        await ResourceHelpers.CopyEmbeddedFileAsync(
            $"{Resources.FeaturesPath}.content_nav.content_nav.css",
            Path.Combine(appDir, Folders.Styles, "features", $"content-nav{FileExtensions.Css}")).ConfigureAwait(false);
        await EnableContentNavCssAsync(appDir).ConfigureAwait(false);
        await EnableContentNavScriptAsync(appDir).ConfigureAwait(false);
        bool updatedPackageJson = await UpdatePackageJsonAsync(
            enableSpa: null,
            enableClientNav: null,
            enableSearch: null,
            enableContentNav: true,
            enableBackend: null,
            enableGithubPages: null,
            mode: null);

        string relativePath = Path.Combine(Folders.Src, Folders.Frontend, Folders.App, Folders.Scripts, "features", $"content-nav{FileExtensions.Ts}");
        Console.WriteLine("Enabled content-nav.");
        Console.WriteLine($"  + {relativePath}");
        if (updatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.enable.contentNav=true");
        }
    }

    private static async Task EnableSearchScriptAsync(string appDir)
    {
        string appTsPath = Path.Combine(appDir, Files.AppTs);
        if (!File.Exists(appTsPath))
        {
            return;
        }

        string source = await File.ReadAllTextAsync(appTsPath).ConfigureAwait(false);
        string updated = EnsureSearchScriptImport(source);
        if (!string.Equals(source, updated, StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(appTsPath, updated).ConfigureAwait(false);
        }
    }

    private static string EnsureSearchScriptImport(string source)
        => EnsureSideEffectImport(source, "./scripts/features/search.js");

    private static string EnsureClientNavScriptImport(string source)
        => EnsureSideEffectImport(source, "./scripts/features/client-nav.js");

    private static string EnsureContentNavScriptImport(string source)
        => EnsureSideEffectImport(source, "./scripts/features/content-nav.js");

    private static string EnsureSideEffectImport(string source, string importPath)
    {
        if (HasSideEffectImport(source, importPath))
        {
            return source;
        }

        return AppendStaticImport(source, importPath);
    }

    private static bool HasSideEffectImport(string source, string importPath)
    {
        string pattern = $@"^\s*import\s+(['""]){Regex.Escape(importPath)}\1\s*;?\s*$";
        return Regex.IsMatch(
            source,
            pattern,
            RegexOptions.Multiline,
            TimeSpan.FromMilliseconds(250));
    }

    private static string AppendStaticImport(string source, string importPath)
    {
        string suffix = source.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? ""
            : Environment.NewLine;

        return source + suffix + $"import \"{importPath}\";{Environment.NewLine}";
    }

    private static async Task EnableSearchCssAsync(string appDir)
    {
        string appCssPath = Path.Combine(appDir, Files.AppCss);
        if (!File.Exists(appCssPath))
        {
            return;
        }

        string css = await File.ReadAllTextAsync(appCssPath).ConfigureAwait(false);
        string updated = css;

        updated = EnsureLayerIncludes(updated, "features");
        updated = EnsureImportIncludes(updated, "./styles/features/search.css", "./styles/components/buttons.css");

        if (!string.Equals(css, updated, StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(appCssPath, updated).ConfigureAwait(false);
        }
    }

    private static async Task EnableContentNavCssAsync(string appDir)
    {
        string appCssPath = Path.Combine(appDir, Files.AppCss);
        if (!File.Exists(appCssPath))
        {
            return;
        }

        string css = await File.ReadAllTextAsync(appCssPath).ConfigureAwait(false);
        string updated = css;

        updated = EnsureLayerIncludes(updated, "features");
        updated = EnsureImportIncludes(updated, "./styles/features/content-nav.css", "./styles/components/buttons.css");

        if (!string.Equals(css, updated, StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(appCssPath, updated).ConfigureAwait(false);
        }
    }

    private static async Task EnsureSearchCssModeEnabledAsync(string appDir)
    {
        string appHtmlPath = Path.Combine(appDir, Files.AppHtml);
        if (!File.Exists(appHtmlPath))
        {
            return;
        }

        string html = await File.ReadAllTextAsync(appHtmlPath).ConfigureAwait(false);
        if (html.Contains("data-webstir-search-styles=", StringComparison.Ordinal))
        {
            return;
        }

        string updated = Regex.Replace(
            html,
            "<html\\b(?![^>]*\\bdata-webstir-search-styles=)",
            "<html data-webstir-search-styles=\"css\"",
            RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(250));

        if (!string.Equals(html, updated, StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(appHtmlPath, updated).ConfigureAwait(false);
        }
    }

    private static async Task EnableContentNavScriptAsync(string appDir)
    {
        string appTsPath = Path.Combine(appDir, Files.AppTs);
        if (!File.Exists(appTsPath))
        {
            return;
        }

        string source = await File.ReadAllTextAsync(appTsPath).ConfigureAwait(false);
        string updated = EnsureContentNavScriptImport(source);
        if (!string.Equals(source, updated, StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(appTsPath, updated).ConfigureAwait(false);
        }
    }

    private static string EnsureLayerIncludes(string css, string layerName)
    {
        Match match = Regex.Match(css, @"@layer\\s+([^;]+);", RegexOptions.None, TimeSpan.FromMilliseconds(250));
        if (!match.Success)
        {
            return css;
        }

        string layerList = match.Groups[1].Value;
        string[] layers = layerList
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (layers.Any(layer => string.Equals(layer, layerName, StringComparison.Ordinal)))
        {
            return css;
        }

        List<string> updated = [.. layers];
        int utilitiesIndex = updated.FindIndex(layer => string.Equals(layer, "utilities", StringComparison.Ordinal));
        int overridesIndex = updated.FindIndex(layer => string.Equals(layer, "overrides", StringComparison.Ordinal));

        int insertIndex = utilitiesIndex >= 0
            ? utilitiesIndex
            : overridesIndex >= 0
                ? overridesIndex
                : updated.Count;

        updated.Insert(insertIndex, layerName);
        string rewritten = $"@layer {string.Join(", ", updated)};";
        return css[..match.Index] + rewritten + css[(match.Index + match.Length)..];
    }

    private static string EnsureImportIncludes(string css, string importPath, string insertAfterImportPath)
    {
        string importNeedle = $"@import \"{importPath}\"";
        if (css.Contains(importNeedle, StringComparison.Ordinal)
            || css.Contains($"@import '{importPath}'", StringComparison.Ordinal))
        {
            return css;
        }

        string insertAfterNeedle = $"@import \"{insertAfterImportPath}\"";
        int insertAfterIndex = css.IndexOf(insertAfterNeedle, StringComparison.Ordinal);
        if (insertAfterIndex < 0)
        {
            insertAfterNeedle = $"@import '{insertAfterImportPath}'";
            insertAfterIndex = css.IndexOf(insertAfterNeedle, StringComparison.Ordinal);
        }

        if (insertAfterIndex >= 0)
        {
            int lineEnd = css.IndexOf('\n', insertAfterIndex);
            int insertAt = lineEnd >= 0 ? lineEnd + 1 : css.Length;
            return css.Insert(insertAt, $"@import \"{importPath}\";{Environment.NewLine}");
        }

        MatchCollection imports = Regex.Matches(
            css,
            @"@import\s+['""][^'""]+['""];?",
            RegexOptions.None,
            TimeSpan.FromMilliseconds(250));
        if (imports.Count > 0)
        {
            Match lastImport = imports[^1];
            int insertAt = lastImport.Index + lastImport.Length;
            string separator = css.Length > insertAt && css[insertAt] == '\n' ? "" : Environment.NewLine;
            return css.Insert(insertAt, $"{separator}@import \"{importPath}\";{Environment.NewLine}");
        }

        return css + $"{Environment.NewLine}@import \"{importPath}\";{Environment.NewLine}";
    }

    private async Task EnableBackendAsync()
    {
        string backendDir = Context.WorkingPath.Combine(Folders.Src).Combine(Folders.Backend);
        if (!Directory.Exists(backendDir))
        {
            string templatePrefix = $"{Resources.TemplatesPath}.full.{Folders.Src}.{Folders.Backend}";
            await ResourceHelpers.CopyEmbeddedDirectoryAsync(templatePrefix, backendDir);
        }

        bool updatedPackageJson = await UpdatePackageJsonAsync(
            enableSpa: null,
            enableClientNav: null,
            enableSearch: null,
            enableContentNav: null,
            enableBackend: true,
            enableGithubPages: null,
            mode: "full");
        EnsureTsReference(Folders.Backend);

        Console.WriteLine("Enabled backend.");
        if (updatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.mode=full, webstir.enable.backend=true");
        }
    }

    private readonly record struct GithubPagesEnableResult(
        string ResolvedBasePath,
        bool UpdatedFrontendConfig,
        bool UpdatedPackageJson);

    private async Task<GithubPagesEnableResult> EnableGithubPagesCoreAsync(string? basePath)
    {
        string resolvedBasePath = ResolveGithubPagesBasePath(basePath);

        string scriptDestination = Context.WorkingPath.Combine(Folders.Utils, Files.DeployGhPagesScript);
        await ResourceHelpers.CopyEmbeddedFileAsync(
            $"{Resources.FeaturesPath}.github_pages.{Files.DeployGhPagesScript}",
            scriptDestination).ConfigureAwait(false);

        bool updatedFrontendConfig = await UpdateFrontendConfigAsync(resolvedBasePath).ConfigureAwait(false);

        bool updatedPackageJson = await UpdatePackageJsonAsync(
            enableSpa: null,
            enableClientNav: null,
            enableSearch: null,
            enableContentNav: null,
            enableBackend: null,
            enableGithubPages: true,
            mode: null).ConfigureAwait(false);

        return new GithubPagesEnableResult(
            ResolvedBasePath: resolvedBasePath,
            UpdatedFrontendConfig: updatedFrontendConfig,
            UpdatedPackageJson: updatedPackageJson);
    }

    private async Task EnableGithubPagesAsync(string? basePath)
    {
        GithubPagesEnableResult result = await EnableGithubPagesCoreAsync(basePath).ConfigureAwait(false);

        Console.WriteLine("Enabled github-pages.");
        Console.WriteLine($"  + {Path.Combine(Folders.Utils, Files.DeployGhPagesScript)}");
        if (result.UpdatedFrontendConfig)
        {
            Console.WriteLine($"  Updated frontend.config.json: publish.basePath={result.ResolvedBasePath}");
        }
        if (result.UpdatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.enable.githubPages=true");
        }
    }

    private async Task EnableGithubDeploymentsAsync(string? basePath)
    {
        GithubPagesEnableResult result = await EnableGithubPagesCoreAsync(basePath).ConfigureAwait(false);

        string workflowDestination = Context.WorkingPath
            .Combine(Folders.Github)
            .Combine(Folders.Workflows)
            .Combine(Files.DeployGhPagesWorkflow);

        await ResourceHelpers.CopyEmbeddedFileAsync(
            $"{Resources.FeaturesPath}.gh_deploy.{Files.DeployGhPagesWorkflow}",
            workflowDestination,
            overwriteExisting: false).ConfigureAwait(false);

        Console.WriteLine("Enabled gh-deploy.");
        Console.WriteLine($"  + {Path.Combine(Folders.Utils, Files.DeployGhPagesScript)}");
        Console.WriteLine($"  + {Path.Combine(Folders.Github, Folders.Workflows, Files.DeployGhPagesWorkflow)}");
        if (result.UpdatedFrontendConfig)
        {
            Console.WriteLine($"  Updated frontend.config.json: publish.basePath={result.ResolvedBasePath}");
        }
        if (result.UpdatedPackageJson)
        {
            Console.WriteLine("  Updated package.json: webstir.enable.githubPages=true");
        }
    }

    private async Task<bool> UpdatePackageJsonAsync(
        bool? enableSpa,
        bool? enableClientNav,
        bool? enableSearch,
        bool? enableContentNav,
        bool? enableBackend,
        bool? enableGithubPages,
        string? mode)
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
        if (enableContentNav.HasValue)
        {
            enable["contentNav"] = enableContentNav.Value;
        }
        if (enableBackend.HasValue)
        {
            enable["backend"] = enableBackend.Value;
        }
        if (enableGithubPages.HasValue)
        {
            enable["githubPages"] = enableGithubPages.Value;
        }

        webstir["enable"] = enable;
        root["webstir"] = webstir;

        if (enableGithubPages == true)
        {
            JsonObject scripts = root["scripts"] as JsonObject ?? new JsonObject();
            const string deployScript = "bash ./utils/deploy-gh-pages.sh";
            if (!scripts.ContainsKey("deploy"))
            {
                scripts["deploy"] = deployScript;
            }
            root["scripts"] = scripts;
        }

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };
        await File.WriteAllTextAsync(packageJsonPath, root.ToJsonString(options) + Environment.NewLine);
        return true;
    }

    private async Task<bool> UpdateFrontendConfigAsync(string basePath)
    {
        string frontendRoot = Context.FrontendPath;
        string configPath = Path.Combine(frontendRoot, "frontend.config.json");
        JsonObject root = new();

        if (File.Exists(configPath))
        {
            try
            {
                JsonNode? parsed = JsonNode.Parse(await File.ReadAllTextAsync(configPath));
                if (parsed is JsonObject parsedRoot)
                {
                    root = parsedRoot;
                }
            }
            catch (JsonException)
            {
                root = new JsonObject();
            }
        }

        JsonObject publish = root["publish"] as JsonObject ?? new JsonObject();
        publish["basePath"] = basePath;
        root["publish"] = publish;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };
        await File.WriteAllTextAsync(configPath, root.ToJsonString(options) + Environment.NewLine);
        return true;
    }

    private string ResolveGithubPagesBasePath(string? basePath)
    {
        string candidate = string.IsNullOrWhiteSpace(basePath) ? Context.WorkspaceName : basePath.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return "/";
        }

        string normalized = candidate.StartsWith("/", StringComparison.Ordinal) ? candidate : "/" + candidate;
        while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }

        return normalized;
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
