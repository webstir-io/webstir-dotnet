using Engine.Extensions;
using Engine.Pipelines.Css.Build;
using Engine.Pipelines.Css.Publish;

namespace Engine.Pipelines.Css;

public class CssHandler(AppWorkspace workspace, CssBuilder builder, CssBundler bundler)
{
    public Task BuildAsync()
    {
        builder.Build();
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
}
