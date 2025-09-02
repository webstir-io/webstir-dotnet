using Engine.Extensions;
using Engine.Pipelines.JavaScript.Build;
using Engine.Pipelines.JavaScript.Publish;

namespace Engine.Pipelines.JavaScript;

public class JsHandler(AppWorkspace workspace, JsBuilder builder, JsBundler bundler)
{
    public Task BuildAsync()
    {
        builder.Build();
        return Task.CompletedTask;
    }

    public Task PublishAsync() => bundler.BundleAsync();

    public Task AddPageAsync(string pageName)
    {
        string pageDirectory = workspace.ClientPagesPath.CreateSubDirectory(pageName);
        string tsFilePath = pageDirectory.Combine($"{Files.Index}.ts");
        string tsContent = $"""
            import '../../{Folders.App}/workspace.js';

            console.log('{pageName} page loaded');
            """;
        
        File.WriteAllText(tsFilePath, tsContent);
        return Task.CompletedTask;
    }
}
