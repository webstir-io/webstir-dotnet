using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Interfaces;
using Engine.Pipelines.JavaScript.Build;
using Engine.Pipelines.JavaScript.Bundling;
using Engine.Pipelines.JavaScript.Common;

using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.JavaScript;

public class JsHandler(AppWorkspace workspace, JsBuilder builder, JsBundler bundler, ILogger<JsHandler> logger) : IPageHandler
{
    private readonly ILogger<JsHandler> _logger = logger;
    public int BuildOrder => 0;
    public int PublishOrder => 0;

    public Task BuildAsync(string? changedFilePath = null)
    {
        DiagnosticCollection diagnostics = new();
        builder.Build(diagnostics);
        LogSummary("JS Build", diagnostics);
        return Task.CompletedTask;
    }

    public async Task PublishAsync()
    {
        DiagnosticCollection diagnostics = new();
        await bundler.BundleAsync(diagnostics);
        LogSummary("JS Publish", diagnostics);
        await CopyAppFilesToDistAsync();
    }

    private async Task CopyAppFilesToDistAsync()
    {
        string sourceApp = workspace.FrontendBuildAppPath;
        string destApp = workspace.FrontendDistAppPath;

        if (!Directory.Exists(sourceApp))
        {
            return;
        }

        Directory.CreateDirectory(destApp);

        foreach (string sourceFile in Directory.GetFiles(sourceApp, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceApp, sourceFile);
            string destination = Path.Combine(destApp, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            if (sourceFile.EndsWith(FileExtensions.Map, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sourceFile.EndsWith(FileExtensions.Js, StringComparison.OrdinalIgnoreCase))
            {
                string content = await File.ReadAllTextAsync(sourceFile);
                content = JsRegex.SourceMapLine().Replace(content, string.Empty);
                content = JsRegex.SourceMapBlock().Replace(content, string.Empty);
                await File.WriteAllTextAsync(destination, content);
            }
            else
            {
                File.Copy(sourceFile, destination, true);
            }
        }
    }

    public Task AddPageAsync(string pageName)
    {
        string pageDirectory = workspace.FrontendPagesPath.CreateSubDirectory(pageName);
        string tsFilePath = pageDirectory.Combine($"{Files.Index}.ts");
        string tsContent = $"""
            import '../../{Folders.App}/app.js';

            console.log('{pageName} page loaded');
            """;

        File.WriteAllText(tsFilePath, tsContent);
        return Task.CompletedTask;
    }

    private void LogSummary(string phase, DiagnosticCollection diagnostics)
    {
        int errorCount = diagnostics.Errors.Count();
        int warningCount = diagnostics.Warnings.Count();
        if (errorCount == 0 && warningCount == 0)
        {
            return;
        }
        if (errorCount > 0)
        {
            _logger.LogError("{Phase} diagnostics: {Errors} errors, {Warnings} warnings", phase, errorCount, warningCount);
        }
        else
        {
            _logger.LogWarning("{Phase} diagnostics: {Warnings} warnings", phase, warningCount);
        }
    }
}
