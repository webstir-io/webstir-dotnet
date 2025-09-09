using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Engine.Helpers;
using Engine.Models;
using Engine.Pipelines.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Engine.Workers;

public partial class ClientWorker(
    AppWorkspace workspace,
    IEnumerable<IFrontendHandler> frontendHandlers,
    ILogger<ClientWorker> logger) : IWorker
{
    private readonly ILogger<ClientWorker> _logger = logger;
    private readonly IEnumerable<IFrontendHandler> _frontendHandlers = frontendHandlers;
    public int BuildOrder => 1;

    public async Task InitAsync(ProjectMode mode) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Templates.ClientPath, workspace.ClientPath);

    public async Task BuildAsync(string? changedFilePath = null)
    {
        if (!string.IsNullOrEmpty(changedFilePath) && !BuildHelpers.ContainsBuildFolder(changedFilePath, Folders.Client))
        {
            return;
        }

        foreach (IGrouping<int, IFrontendHandler> group in _frontendHandlers
                     .GroupBy(h => h.BuildOrder)
                     .OrderBy(g => g.Key))
        {
            List<Task> tasks = [];
            foreach (IFrontendHandler handler in group)
            {
                tasks.Add(handler.BuildAsync(changedFilePath));
            }
            await Task.WhenAll(tasks);
        }
    }

    public async Task PublishAsync()
    {
        if (Directory.Exists(workspace.ClientDistPath))
        {
            TryClearDirectory(workspace.ClientDistPath);
        }
        else
        {
            Directory.CreateDirectory(workspace.ClientDistPath);
        }
        
        foreach (IGrouping<int, IFrontendHandler> group in _frontendHandlers
                     .GroupBy(h => h.PublishOrder)
                     .OrderBy(g => g.Key))
        {
            List<Task> tasks = [];
            foreach (IFrontendHandler handler in group)
            {
                tasks.Add(handler.PublishAsync());
            }
            await Task.WhenAll(tasks);
        }
    }

    public async Task AddPageAsync(string pageName)
    {
        List<Task> tasks = [];
        foreach (IPageHandler handler in _frontendHandlers.OfType<IPageHandler>())
        {
            tasks.Add(handler.AddPageAsync(pageName));
        }
        await Task.WhenAll(tasks);
    }
    
    private void TryClearDirectory(string path)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file during cleanup: {File}", file);
                }
            }

            foreach (string dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete directory during cleanup: {Directory}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear directory: {Path}", path);
        }
    }
}
