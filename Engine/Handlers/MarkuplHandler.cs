using Engine.Building.Html;
using Engine.Bundling.Html;
using Engine.Extensions;

namespace Engine.Handlers;

public class MarkupHandler(AppWorkspace workspace, HtmlBuilder htmlBuilder, HtmlBundler htmlBundler)
{

    public async Task BuildAsync()
    {
        await htmlBuilder.BuildAsync();
    }

    public async Task PublishAsync()
    {
        await htmlBundler.BundleAsync();
    }

    public async Task AddPageAsync(string pageName)
    {
        string pagePath = workspace.ClientPagesPath.Combine(pageName);
        pagePath.Create();
        string htmlContent = GeneratePageTemplate(pageName);
        string outputPath = pagePath.Combine($"{Files.Index}{FileExtensions.Html}");
        await File.WriteAllTextAsync(outputPath, htmlContent);
    }

    private static string GeneratePageTemplate(string pageName)
    {
        return $"""
        <head>
            <title>{pageName}</title>
            <link rel="stylesheet" href="{Files.Index}{FileExtensions.Css}" />
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