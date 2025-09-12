using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Interfaces;
using Engine.Pipelines.Core.Utilities;
using Engine.Pipelines.JavaScript.Build;
using Engine.Pipelines.JavaScript.Bundling;
using Engine.Pipelines.JavaScript.Common;
using Engine.Pipelines.JavaScript.Minification;

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
        await CopyErrorToDistAsync();
    }

    private async Task CopyErrorToDistAsync()
    {
        string sourceErrorJs = workspace.FrontendBuildAppPath.Combine("error.js");
        if (!File.Exists(sourceErrorJs))
        {
            return;
        }

        string destApp = workspace.FrontendDistAppPath;
        Directory.CreateDirectory(destApp);

        // Minify content and fingerprint
        string content = await File.ReadAllTextAsync(sourceErrorJs);
        content = JsRegex.SourceMapLine().Replace(content, string.Empty);
        content = JsRegex.SourceMapBlock().Replace(content, string.Empty);
        content = JsMinifier.Minify(content);

        string hash = ContentHashGenerator.ComputeHash(content);
        string hashedFileName = $"error.{hash}{FileExtensions.Js}";
        string hashedPath = Path.Combine(destApp, hashedFileName);
        await File.WriteAllTextAsync(hashedPath, content);
        await Precompression.CreatePrecompressedVariantsAsync(hashedPath);

        // Keep a stable path for error templates that reference /app/error.js
        string stablePath = Path.Combine(destApp, "error.js");
        await File.WriteAllTextAsync(stablePath, content);
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
