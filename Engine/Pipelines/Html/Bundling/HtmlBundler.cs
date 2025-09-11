using System.IO;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Utilities;
using CssConstants = Engine.Pipelines.Css.Common.Css;
using Engine.Pipelines.Html.Common;
using Engine.Pipelines.Html.Parsing;
using Engine.Pipelines.Html.Transformation;
using Engine.Pipelines.Html.Minification;

namespace Engine.Pipelines.Html.Bundling;

public class HtmlBundler(AppWorkspace workspace)
{

    public async Task BundleAsync(DiagnosticCollection? diagnostics = null) => await BundlePageHtmlAsync(diagnostics);

    private async Task BundlePageHtmlAsync(DiagnosticCollection? diagnostics)
    {
        string pagesPath = workspace.FrontendBuildPath.Combine(Folders.Pages);
        if (!pagesPath.Exists())
        {
            return;
        }

        foreach (string pageDir in pagesPath.Folders())
        {
            string pageName = pageDir.Filename();
            string pageHtml = pageDir.Combine($"{Files.Index}{FileExtensions.Html}");

            if (!pageHtml.Exists())
            {
                diagnostics?.Add(new Diagnostic { Level = DiagnosticLevel.Warning, Message = $"Missing page HTML: {pageHtml}", File = pageHtml });
                continue;
            }

            await ProcessHtmlFileAsync(pageHtml, pageName, diagnostics);
        }
    }

    private async Task ProcessHtmlFileAsync(string sourceFile, string pageName, DiagnosticCollection? diagnostics)
    {
        string htmlContent = await File.ReadAllTextAsync(sourceFile);

        htmlContent = HtmlParser.RemoveRefreshScript(htmlContent);

        // Rewrite asset references using per-page manifest if present
        string pageDistDir = workspace.FrontendDistPath.Combine(Folders.Pages, pageName);
        AssetManifest manifest = AssetManifest.Load(pageDistDir);
        htmlContent = HtmlTransformer.RewriteAssetReferences(htmlContent, manifest, pageName);

        // Inject width/height on <img> tags based on build image files
        string pageBuildDir = workspace.FrontendBuildPath.Combine(Folders.Pages, pageName);
        htmlContent = HtmlTransformer.AddImageDimensions(htmlContent, pageBuildDir);

        // Add SRI for external scripts/styles (best-effort, network dependent)
        htmlContent = await HtmlSecurityEnhancer.AddSRIForExternalResourcesAsync(htmlContent);

        string originalHtml = htmlContent;
        try
        {
            htmlContent = HtmlMinifier.Minify(htmlContent);
        }
        catch (System.Exception ex)
        {
            diagnostics?.Add(new Diagnostic
            {
                Level = DiagnosticLevel.Warning,
                Message = $"HTML minification failed: {ex.Message}",
                File = sourceFile
            });
            htmlContent = originalHtml; // graceful fallback
        }

        string distPagePath = workspace.FrontendDistPath.Combine(Folders.Pages, pageName, $"{Files.Index}{FileExtensions.Html}");
        distPagePath.DirectoryName().Create();

        await File.WriteAllTextAsync(distPagePath, htmlContent);

        // Create precompressed variants for transport (Brotli and gzip)
        await Precompression.CreatePrecompressedVariantsAsync(distPagePath);
    }

}
