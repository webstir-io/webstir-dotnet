using Engine.Handlers;
using Engine.Models;

namespace Engine.Workers;

public class ClientWorker(
    HtmlHandler htmlHandler,
    CssHandler cssHandler,
    ScriptsHandler scriptsHandler,
    ImagesHandler imagesHandler) : IWorker
{
    public int BuildOrder => 1;

    public async Task InitAsync(ProjectMode mode)
    {
        await Task.WhenAll(
            htmlHandler.InitAsync(mode),
            cssHandler.InitAsync(mode),
            scriptsHandler.InitAsync(mode),
            imagesHandler.InitAsync(mode)
        );
    }

    public async Task BuildAsync(bool releaseMode)
    {
        // Scripts first (heavy TypeScript compilation)
        await scriptsHandler.BuildAsync(releaseMode);
        
        // Then parallel execution of fast handlers
        await Task.WhenAll(
            htmlHandler.BuildAsync(releaseMode),
            cssHandler.BuildAsync(releaseMode),
            imagesHandler.BuildAsync(releaseMode)
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