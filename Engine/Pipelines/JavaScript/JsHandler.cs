using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Interfaces;
using Engine.Pipelines.JavaScript.Build;
using Engine.Pipelines.JavaScript.Publish;

using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.JavaScript;

public class JsHandler(AppWorkspace workspace, JsBuilder builder, JsBundler bundler, JsPublisher publisher, ILogger<JsHandler> logger) : IPageHandler
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
        await publisher.PublishAsync();
    }

    public Task AddPageAsync(string pageName)
    {
        string pageDirectory = workspace.FrontendPagesPath.CreateSubDirectory(pageName);
        string tsFilePath = pageDirectory.Combine($"{Files.Index}.ts");
        string tsContent = $"""
            import '../../{Folders.App}/workspace.js';

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
