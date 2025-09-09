using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Interfaces;
using Engine.Pipelines.Css.Build;
using Engine.Pipelines.Css.Publish;

using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Css;

public class CssHandler(AppWorkspace workspace, CssBuilder builder, CssBundler bundler, ILogger<CssHandler> logger) : IPageHandler
{
    private readonly ILogger<CssHandler> _logger = logger;
    public int BuildOrder => 1;
    public int PublishOrder => 0;

    public Task BuildAsync(string? changedFilePath = null)
    {
        DiagnosticCollection diagnostics = new();
        builder.Build(diagnostics);
        LogSummary("CSS Build", diagnostics);
        return Task.CompletedTask;
    }

    public Task PublishAsync() => bundler.BundleAsync();

    public Task AddPageAsync(string pageName)
    {
        string cssContent = $"""
            /* {pageName} Page Styles */
            @import "@app/app.css";

            /* Add your page-specific styles here */

            """;
        string pageDirectory = workspace.ClientPagesPath.Combine(pageName);
        string cssFilePath = pageDirectory.Combine($"{Files.Index}.css");
        File.WriteAllText(cssFilePath, cssContent);
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
