using Engine.Handlers;
using Engine.Helpers;
using Engine.Models;

namespace Engine.Workers;

public class ClientWorker(
    AppWorkspace workspace,
    MarkupHandler htmlHandler,
    StylesHandler cssHandler,
    ScriptsHandler scriptsHandler,
    ImagesHandler imagesHandler) : IWorker
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
            ImagesHandler.AddPageAsync(pageName)
        );
    }
}