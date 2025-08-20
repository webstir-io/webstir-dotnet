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

    public async Task StartAsync(AppWorkspace workspace, Func<bool, Task>? onChangeAction = null)
    {
        _logger.LogInformation("Starting {DevService} for {WorkspacePath}", App.DevService, workspace.WorkingPath);
        
        try
        {
            await _webServer.StartAsync(workspace);
            await _nodeServer.StartAsync(workspace);            
            await _changeService.Initialize(workspace, onChangeAction, RestartNodeServerAsync, NotifyClientsAsync);
            await _changeService.StartAsync();            
            await _watchService.Watch(workspace);
            
            // Wait for exit signal and ensure proper cleanup
            await WaitForExitSignalAsync();
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
        _logger.LogInformation("Restarting Node server...");
        await _nodeServer.StopAsync();
        await _nodeServer.StartAsync(workspace);
    }

    public async Task NotifyClientsAsync()
    {
        await _webServer.UpdateClientsAsync();
    }

    private async Task WaitForExitSignalAsync()
    {
        var exitEvent = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            exitEvent.SetResult(true);
        };
        
        _logger.LogInformation("{DevService} is running. Press Ctrl+C to exit.", App.DevService);

        await exitEvent.Task;
    }
}