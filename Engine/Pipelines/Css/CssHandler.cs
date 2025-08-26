using Engine.Extensions;
using Engine.Pipelines.Css.Build;
using Engine.Pipelines.Css.Publish;

namespace Engine.Pipelines.Css;

public class CssHandler(AppWorkspace workspace, CssBuilder builder, CssBundler bundler)
{
    public async Task BuildAsync()
    {
        builder.Build();
        await Task.CompletedTask;
    }

    public async Task PublishAsync() => await bundler.BundleAsync();

    public async Task AddPageAsync(string pageName)
    {
        string cssContent = $"""
            /* {pageName} Page Styles */
            @import "@app/app.css";
            
            /* Add your page-specific styles here */
            
            """;
        string pageDirectory = workspace.ClientPagesPath.Combine(pageName);
        string cssFilePath = pageDirectory.Combine($"{Files.Index}.css");
        File.WriteAllText(cssFilePath, cssContent);
        
        await Task.CompletedTask;
    }
}