using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core.Interfaces;
using Engine.Pipelines.Core.Utilities;
using Engine.Pipelines.Html.Build;
using Engine.Pipelines.Html.Bundling;

using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Html;

public class HtmlHandler(AppWorkspace workspace, HtmlBuilder htmlBuilder, HtmlBundler htmlBundler, ILogger<HtmlHandler> logger) : IPageHandler
{
    private readonly ILogger<HtmlHandler> _logger = logger;
    public int BuildOrder => 1;
    public int PublishOrder => 1;

    public async Task BuildAsync(string? changedFilePath = null)
    {
        DiagnosticCollection diagnostics = new();
        await htmlBuilder.BuildAsync(diagnostics);
        LogSummary("HTML Build", diagnostics);
    }

    public async Task PublishAsync()
    {
        DiagnosticCollection diagnostics = new();
        await htmlBundler.BundleAsync(diagnostics);
        LogSummary("HTML Publish", diagnostics);
    }

    public async Task AddPageAsync(string pageName)
    {
        string pagePath = workspace.FrontendPagesPath.Combine(pageName);
        pagePath.Create();
        string htmlContent = GeneratePageTemplate(pageName);
        string outputPath = pagePath.Combine($"{Files.Index}{FileExtensions.Html}");
        await File.WriteAllTextAsync(outputPath, htmlContent);
    }

    private static string GeneratePageTemplate(string pageName)
    {
        // Use raw string literal without escaping quotes to avoid outputting literal backslashes
        return $"""
        <head>
            <title>{pageName}</title>
            <link rel="stylesheet" href="{Files.Index}{FileExtensions.Css}">
        </head>
        <body>
            <main>
                <h1>{pageName}</h1>
                <p>Content for the {pageName} page.</p>
            </main>
            <script type="module" src="{Files.Index}{FileExtensions.Js}" async></script>
        </body>
        """;
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
