using Engine.Helpers;
using Engine.Models;
using Engine.Pipelines.Assets;
using Engine.Pipelines.Css;
using Engine.Pipelines.Html;
using Engine.Pipelines.JavaScript;

namespace Engine.Workers;

public class ClientWorker(
    AppWorkspace workspace,
    HtmlHandler htmlHandler,
    CssHandler cssHandler,
    JsHandler scriptsHandler,
    AssetHandler imagesHandler) : IWorker
{
    public int BuildOrder => 1;

    public async Task InitAsync(ProjectMode mode)
    {
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Templates.ClientPath, workspace.ClientPath);
    }

    public async Task BuildAsync(string? changedFilePath = null)
    {
        if (!string.IsNullOrEmpty(changedFilePath) && !BuildHelpers.ContainsBuildFolder(changedFilePath, Folders.Client))
            return;
        
        await scriptsHandler.BuildAsync();
        await Task.WhenAll(            
            htmlHandler.BuildAsync(),
            cssHandler.BuildAsync(),
            imagesHandler.BuildAsync()
        );
    }

    public async Task PublishAsync()
    {
        if (Directory.Exists(workspace.ClientDistPath))
            Directory.Delete(workspace.ClientDistPath, recursive: true);
        
        Directory.CreateDirectory(workspace.ClientDistPath);
        
        await Task.WhenAll(
            htmlHandler.PublishAsync(),
            cssHandler.PublishAsync(),
            scriptsHandler.PublishAsync(),
            imagesHandler.PublishAsync()
        );
    }

    public async Task AddPageAsync(string pageName)
    {
        await Task.WhenAll(
            htmlHandler.AddPageAsync(pageName),
            cssHandler.AddPageAsync(pageName),
            scriptsHandler.AddPageAsync(pageName),
            AssetHandler.AddPageAsync(pageName)
        );
    }
}