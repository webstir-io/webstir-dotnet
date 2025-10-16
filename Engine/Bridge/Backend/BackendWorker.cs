using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Engine.Bridge.Module;
using Engine.Extensions;
using Engine.Helpers;
using Engine.Interfaces;
using Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Engine.Bridge.Backend;

public class BackendWorker(AppWorkspace workspace, IOptions<AppSettings> options, ILogger<BackendWorker> logger) : IWorkflowWorker
{
    private readonly AppSettings _settings = options.Value;
    private const string _tsConfigFile = "tsconfig.json";
    private readonly IBackendModuleProviderResolver _moduleProviderResolver = new DefaultBackendModuleProviderResolver();
    private BackendModuleProvider? _resolvedProvider;
    private readonly ILogger<BackendWorker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public int BuildOrder => 2;

    public async Task InitAsync(ProjectMode mode = ProjectMode.Fullstack) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.BackendPath, workspace.BackendPath);

    public async Task BuildAsync(string? changedFilePath = null)
    {
        if (!string.IsNullOrEmpty(changedFilePath) && !BuildHelpers.ContainsBuildFolder(changedFilePath, Folders.Backend))
        {
            return;
        }

        string packageJsonPath = workspace.WorkingPath.Combine(Files.PackageJson);
        if (File.Exists(packageJsonPath))
        {
            NpmHelper.RunNpmInstall(workspace.WorkingPath);
        }

        BackendModuleProvider provider = await EnsureProviderAsync();

        Dictionary<string, string?> env = new(StringComparer.Ordinal)
        {
            ["API_PORT"] = _settings.ApiServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["WEB_PORT"] = _settings.WebServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        ModuleBuildExecutionResult result = await ModuleBuildExecutor.ExecuteAsync(
            workspace,
            provider.Id,
            ModuleBuildMode.Build,
            env,
            _logger,
            CancellationToken.None);

        LogBackendManifest("Build", result);
    }

    public async Task PublishAsync()
    {
        BackendModuleProvider provider = await EnsureProviderAsync();
        Dictionary<string, string?> env = new(StringComparer.Ordinal)
        {
            ["API_PORT"] = _settings.ApiServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["WEB_PORT"] = _settings.WebServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        ModuleBuildExecutionResult result = await ModuleBuildExecutor.ExecuteAsync(
            workspace,
            provider.Id,
            ModuleBuildMode.Publish,
            env,
            _logger,
            CancellationToken.None);

        foreach (string jsFilepath in Directory.GetFiles(workspace.BackendBuildPath, "*.js", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(workspace.BackendBuildPath, jsFilepath);
            string targetFilePath = Path.Combine(workspace.BackendDistPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);

            string jsContent = File.ReadAllText(jsFilepath);
            jsContent = RemoveJavaScriptComments(jsContent);

            File.WriteAllText(targetFilePath, jsContent);
        }

        LogBackendManifest("Publish", result);
    }

    private static string RemoveJavaScriptComments(string js)
    {
        string singleLinePattern = @"(?<!:)//.*$";
        js = System.Text.RegularExpressions.Regex.Replace(
            js,
            singleLinePattern,
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        string multiLinePattern = @"/\*[\s\S]*?\*/";
        js = System.Text.RegularExpressions.Regex.Replace(js, multiLinePattern, string.Empty);

        string emptyLinePattern = @"^\s*\r?\n";
        js = System.Text.RegularExpressions.Regex.Replace(
            js,
            emptyLinePattern,
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Multiline
        );

        return js.Trim();
    }

    private async Task<BackendModuleProvider> EnsureProviderAsync()
    {
        if (_resolvedProvider is not null)
        {
            return _resolvedProvider;
        }

        _resolvedProvider = await _moduleProviderResolver.ResolveAsync(workspace, CancellationToken.None);
        return _resolvedProvider;
    }

    private void LogBackendManifest(string stage, ModuleBuildExecutionResult result)
    {
        _logger.LogInformation(
            "[backend] {Stage} provider {ProviderId} produced {EntryCount} entry point(s).",
            stage,
            result.Provider.Id,
            result.Manifest.EntryPoints.Count);

        foreach (ModuleDiagnostic diagnostic in result.Manifest.Diagnostics)
        {
            if (string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[backend] {Message}", diagnostic.Message);
            }
            else if (string.Equals(diagnostic.Severity, "warn", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[backend] {Message}", diagnostic.Message);
            }
            else
            {
                _logger.LogInformation("[backend] {Message}", diagnostic.Message);
            }
        }

        foreach (ModuleLogEvent evt in result.Events)
        {
            LogModuleEvent(evt);
        }
    }

    private void LogModuleEvent(ModuleLogEvent evt)
    {
        if (string.Equals(evt.Type, "error", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("[backend] {Message}", evt.Message);
        }
        else if (string.Equals(evt.Type, "warn", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[backend] {Message}", evt.Message);
        }
        else
        {
            _logger.LogInformation("[backend] {Message}", evt.Message);
        }
    }

}
