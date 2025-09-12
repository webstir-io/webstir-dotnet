using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Core.Utilities;
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

        string pageDistDir = workspace.FrontendDistPath.Combine(Folders.Pages, pageName);
        AssetManifest manifest = AssetManifest.Load(pageDistDir);
        htmlContent = HtmlTransformer.RewriteAssetReferences(htmlContent, manifest, pageName);

        // Rewrite shared error script to fingerprinted filename if available
        string? hashedError = TryGetHashedErrorScriptFileName();
        if (!string.IsNullOrWhiteSpace(hashedError))
        {
            string original = "/" + Folders.App + "/error" + FileExtensions.Js;
            string replacement = "/" + Folders.App + "/" + hashedError;
            htmlContent = htmlContent.Replace("\"" + original + "\"", "\"" + replacement + "\"");
            htmlContent = htmlContent.Replace("'" + original + "'", "'" + replacement + "'");
        }

        string pageBuildDir = workspace.FrontendBuildPath.Combine(Folders.Pages, pageName);
        htmlContent = HtmlTransformer.AddImageDimensions(htmlContent, pageBuildDir);
        htmlContent = Images.LazyLoadEnhancer.AddLazyLoading(htmlContent);
        htmlContent = await HtmlSecurityEnhancer.AddSRIForExternalResourcesAsync(htmlContent);
        htmlContent = ResourceHintInjector.Inject(htmlContent, manifest, pageName, workspace);
        htmlContent = Css.CriticalCssExtractor.InlineCriticalCss(htmlContent, pageName, pageDistDir);

        if (!string.IsNullOrWhiteSpace(manifest.Css))
        {
            string cssPath = Path.Combine(pageDistDir, manifest.Css);
            htmlContent = FontPreloadInjector.InjectFromCss(htmlContent, cssPath, pageName);
        }

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
            htmlContent = originalHtml;
        }

        string distPagePath = workspace.FrontendDistPath.Combine(Folders.Pages, pageName, $"{Files.Index}{FileExtensions.Html}");
        distPagePath.DirectoryName().Create();

        await File.WriteAllTextAsync(distPagePath, htmlContent);
        await Precompression.CreatePrecompressedVariantsAsync(distPagePath);
    }

    private string? TryGetHashedErrorScriptFileName()
    {
        string appDistDir = workspace.FrontendDistPath.Combine(Folders.App);
        if (!appDistDir.Exists())
        {
            return null;
        }

        try
        {
            string[] candidates = Directory.GetFiles(appDistDir, "error.*.js", SearchOption.TopDirectoryOnly);
            // Prefer a single candidate; if multiple, take the latest modified
            if (candidates.Length == 0)
            {
                return null;
            }
            string chosen = candidates
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .First();
            return Path.GetFileName(chosen);
        }
        catch
        {
            return null;
        }
    }

}
