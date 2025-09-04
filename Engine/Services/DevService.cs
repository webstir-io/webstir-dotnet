using System;
using System.Threading.Tasks;
using System.Threading;
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

    public async Task StartAsync(AppWorkspace workspace, Func<string?, bool, Task>? onChangeAction = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _logger.LogInformation("Starting {DevService} for {WorkspacePath}", App.DevService, workspace.WorkingPath);

        try
        {
            await _webServer.StartAsync(workspace, cancellationToken);
            await _nodeServer.StartAsync(workspace, cancellationToken);
            await _changeService.Initialize(workspace, onChangeAction, RestartNodeServerAsync, NotifyClientsAsync);
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

    public async Task NotifyClientsAsync() => await _webServer.UpdateClientsAsync();

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
