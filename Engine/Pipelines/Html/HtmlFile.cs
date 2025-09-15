using System.IO;

namespace Engine.Pipelines.Html;

public sealed class HtmlFile(string filepath)
{
    private string html = File.ReadAllText(filepath);

    public string Html => html;

    public string Merge(string pageHtml) => HtmlTransformer.MergeTemplates(html, pageHtml);

    public void Remove(string markup) => html = html.Replace(markup, string.Empty);
}
