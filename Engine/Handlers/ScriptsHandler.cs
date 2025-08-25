using Engine.Building.JavaScript;
using Engine.Bundling.JavaScript;
using Engine.Extensions;

namespace Engine.Handlers;

public class ScriptsHandler(AppWorkspace workspace, JsBuilder builder, JsBundler bundler)
{
    public async Task BuildAsync()
    {
        builder.Build();
        await Task.CompletedTask;
    }

    public async Task PublishAsync() => await bundler.BundleAsync();

    public async Task AddPageAsync(string pageName)
    {
        string pageDirectory = workspace.ClientPagesPath.CreateSubDirectory(pageName);
        string tsFilePath = pageDirectory.Combine($"{Files.Index}.ts");
        string tsContent = $"""
            import '../../{Folders.App}/workspace.js';

            console.log('{pageName} page loaded');
            """;
        
        File.WriteAllText(tsFilePath, tsContent);
        
        await Task.CompletedTask;
    }
}