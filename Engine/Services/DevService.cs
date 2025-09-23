using System;
using System.Threading.Tasks;
using System.Threading;
using Engine.Models;
using Engine.Servers;

using Microsoft.Extensions.Logging;

namespace Engine.Services;

public class DevService(
    WatchService watchService,
    ChangeService changeService,
    WebServer webServer,
    NodeServer nodeServer,
    ILogger<DevService> logger)
{
    private readonly WatchService _watchService = watchService;
    private readonly ChangeService _changeService = changeService;
    private readonly WebServer _webServer = webServer;
    private readonly NodeServer _nodeServer = nodeServer;
    private readonly ILogger<DevService> _logger = logger;

    public async Task StartAsync(AppWorkspace workspace, Func<string?, bool, Task<ChangeProcessingResult>>? onChangeAction = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _logger.LogInformation("Starting {DevService} for {WorkspacePath}", App.DevService, workspace.WorkingPath);

        try
        {
            await _webServer.StartAsync(workspace, cancellationToken);
            await _nodeServer.StartAsync(workspace, cancellationToken);
            await _changeService.Initialize(workspace, onChangeAction, RestartNodeServerAsync, NotifyClientsAsync, PublishHotUpdateAsync);
            await _changeService.StartAsync();
            await _watchService.Watch(workspace);

            // Wait for exit signal and ensure proper cleanup
            await WaitForExitSignalAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{DevService} failed to start: {Message}", App.DevService, ex.Message);
            await StopAsync();
            throw;
        }
        finally
        {
            await StopAsync();
        }
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping {DevService}...", App.DevService);

        try
        {
            _watchService.Stop();
            await _changeService.StopAsync();
            await Task.WhenAll(
                _webServer.StopAsync(),
                _nodeServer.StopAsync()
            );

            _logger.LogInformation("{DevService} stopped", App.DevService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping {DevService}: {Message}", App.DevService, ex.Message);
        }
    }

    public async Task RestartNodeServerAsync(AppWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _logger.LogInformation("Restarting Node server...");
        await _nodeServer.StopAsync();
        await _nodeServer.StartAsync(workspace);
    }

    public async Task NotifyClientsAsync(ClientNotificationType type)
    {
        switch (type)
        {
            case ClientNotificationType.BuildStarting:
                await _webServer.PublishStatusAsync("building");
                break;
            case ClientNotificationType.BuildSucceeded:
                await _webServer.PublishStatusAsync("success");
                break;
            case ClientNotificationType.BuildFailed:
                await _webServer.PublishStatusAsync("error");
                break;
            case ClientNotificationType.Reload:
                await _webServer.UpdateClientsAsync();
                break;
            case ClientNotificationType.HotUpdate:
                break;
        }
    }

    private async Task PublishHotUpdateAsync(FrontendHotUpdate hotUpdate)
    {
        if (hotUpdate.RequiresReload)
        {
            _logger.LogDebug(
                "Hot update requires reload for {ChangedFile}; falling back to full reload.",
                hotUpdate.ChangedFile ?? "unknown");
            await _webServer.UpdateClientsAsync();
            return;
        }

        _logger.LogDebug(
            "Queued hot update with {ModuleCount} modules and {StyleCount} styles.",
            hotUpdate.Modules.Count,
            hotUpdate.Styles.Count);

        await _webServer.UpdateClientsAsync();
    }

    private async Task WaitForExitSignalAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> exitEvent = new();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            exitEvent.SetResult(true);
        };

        _logger.LogInformation("{DevService} is running. Press Ctrl+C to exit.", App.DevService);
        try
        {
            await Task.WhenAny(exitEvent.Task, Task.Delay(Timeout.Infinite, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // cancelled
        }
    }
}
