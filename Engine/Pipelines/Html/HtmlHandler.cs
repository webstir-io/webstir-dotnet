using System;
using System.IO;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core.Interfaces;
using Engine.Pipelines.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Engine.Pipelines.Html;

public class HtmlHandler(AppWorkspace workspace, HtmlBuilder htmlBuilder, ILogger<HtmlHandler> logger) : IPageHandler
{
    private readonly ILogger<HtmlHandler> _logger = logger;
    public int BuildOrder => 1;
    public int PublishOrder => 1;

    public async Task<bool> BuildAsync(string? changedFilePath = null) =>
        await htmlBuilder.BuildAsync();

    public async Task<bool> PublishAsync() =>
        await htmlBuilder.PublishAsync();

    public async Task<bool> AddPageAsync(string pageName)
    {
        try
        {
            string pagePath = workspace.FrontendPagesPath.Combine(pageName);
            pagePath.Create();
            string htmlContent = GeneratePageTemplate(pageName);
            string outputPath = pagePath.Combine($"{Files.Index}{FileExtensions.Html}");
            await File.WriteAllTextAsync(outputPath, htmlContent);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("[HTML] Error creating page {PageName} - {Message}", pageName, ex.Message);
            return false;
        }
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

}
