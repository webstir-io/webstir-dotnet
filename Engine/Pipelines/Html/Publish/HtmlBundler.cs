using System.IO;
using System.Threading.Tasks;
using Engine.Extensions;
using Engine.Pipelines.Core;
using Engine.Pipelines.Html.Constants;

namespace Engine.Pipelines.Html.Publish;

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

        htmlContent = HtmlRegex.RefreshScript().Replace(htmlContent, string.Empty);
        htmlContent = HtmlRegex.Comment().Replace(htmlContent, string.Empty);

        // Rewrite asset references using per-page manifest if present
        string pageDistDir = workspace.FrontendDistPath.Combine(Folders.Pages, pageName);
        AssetManifest manifest = AssetManifest.Load(pageDistDir);
        htmlContent = RewriteAssetReferences(htmlContent, manifest, pageName);

        htmlContent = MinifyHtml(htmlContent);

        string distPagePath = workspace.FrontendDistPath.Combine(Folders.Pages, pageName, $"{Files.Index}{FileExtensions.Html}");
        distPagePath.DirectoryName().Create();

        await File.WriteAllTextAsync(distPagePath, htmlContent);
    }


    private static string MinifyHtml(string html)
    {
        // Be safe: only collapse whitespace between tags; avoid touching content
        // inside <script>, <style>, <pre>, <textarea>
        html = HtmlRegex.InterTagWhitespace().Replace(html, "><");
        return html.Trim();
    }

    private static string RewriteAssetReferences(string html, AssetManifest manifest, string pageName)
    {
        string result = html;

        if (!string.IsNullOrWhiteSpace(manifest.Css))
        {
            string cssQuoted = $"/{Folders.Pages}/{pageName}/{manifest.Css}";
            result = result.Replace($"\"{Files.Index}{FileExtensions.Css}\"", $"\"{cssQuoted}\"");
            result = result.Replace($"'{Files.Index}{FileExtensions.Css}'", $"'{cssQuoted}'");
            // Also replace module variant if present
            result = result.Replace($"\"{Files.Index}{Css.Css.ModuleExt}\"", $"\"{cssQuoted}\"");
            result = result.Replace($"'{Files.Index}{Css.Css.ModuleExt}'", $"'{cssQuoted}'");
        }

        if (!string.IsNullOrWhiteSpace(manifest.Js))
        {
            string jsQuoted = $"/{Folders.Pages}/{pageName}/{manifest.Js}";
            result = result.Replace($"\"{Files.Index}{FileExtensions.Js}\"", $"\"{jsQuoted}\"");
            result = result.Replace($"'{Files.Index}{FileExtensions.Js}'", $"'{jsQuoted}'");
        }

        return result;
    }
}
