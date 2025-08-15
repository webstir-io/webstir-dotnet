using Engine.Handlers;
using Engine.Helpers;
using Engine.Models;

namespace Engine.Workers;

public class ClientWorker(
    AppContext context,
    HtmlHandler htmlHandler,
    CssHandler cssHandler,
    ScriptsHandler scriptsHandler,
    ImagesHandler imagesHandler) : IWorker
{
    public int BuildOrder => 1;

    public async Task InitAsync(ProjectMode mode)
    {
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Resources.ClientResourcesPath, context.ClientPath);
    }

    public async Task BuildAsync()
    {
        await scriptsHandler.BuildAsync();        
        await Task.WhenAll(
            htmlHandler.BuildAsync(),
            cssHandler.BuildAsync(),
            imagesHandler.BuildAsync()
        );
    }

    public async Task PublishAsync()
    {
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
            imagesHandler.AddPageAsync(pageName)
        );
    }
}