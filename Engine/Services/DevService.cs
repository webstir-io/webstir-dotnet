using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Engine.Bridge.Backend;
using Engine.Bridge.Module;
using Engine.Extensions;
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
        _logger.LogInformation("Starting {DevService} for {Workspace}", App.DevService, workspace.WorkspaceName);

        try
        {
            await _webServer.StartAsync(workspace, cancellationToken);
            WorkspaceProfile profile = workspace.DetectWorkspaceProfile();
            bool hasBackendSource = workspace.BackendPath.Exists();
            if (hasBackendSource && profile.HasBackend)
            {
                await _nodeServer.StartAsync(workspace, cancellationToken);
            }
            else if (profile.HasBackend)
            {
                _logger.LogWarning("Backend source expected but not found. Skipping Node.js server.");
            }
            else
            {
                _logger.LogDebug("Backend not present for frontend-only project; skipping Node.js server.");
            }
            await _changeService.Initialize(workspace, onChangeAction, RestartNodeServerAsync, NotifyClientsAsync, PublishHotUpdateAsync);
            await InspectBackendManifestAsync(workspace, cancellationToken);
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
        _logger.LogDebug("Stopping {DevService}...", App.DevService);

        try
        {
            _watchService.Stop();
            await _changeService.StopAsync();
            await Task.WhenAll(
                _webServer.StopAsync(),
                _nodeServer.StopAsync()
            );

            _logger.LogInformation("Stopping {DevService}... stopped.", App.DevService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping {DevService}: {Message}", App.DevService, ex.Message);
        }
    }

    public async Task RestartNodeServerAsync(AppWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (!workspace.BackendPath.Exists())
        {
            _logger.LogDebug("Backend source still missing; skipping Node server restart.");
            return;
        }

        _logger.LogInformation("Restarting Node server...");
        await _nodeServer.StopAsync();
        await _nodeServer.StartAsync(workspace);
        await InspectBackendManifestAsync(workspace, CancellationToken.None);
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

            await _webServer.PublishStatusAsync("hmr-fallback");
            await _webServer.UpdateClientsAsync();
            return;
        }

        _logger.LogDebug(
            "Streaming hot update with {ModuleCount} modules and {StyleCount} styles.",
            hotUpdate.Modules.Count,
            hotUpdate.Styles.Count);

        try
        {
            await _webServer.PublishHotUpdateAsync(hotUpdate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to stream hot update for {ChangedFile}; falling back to reload.",
                hotUpdate.ChangedFile ?? "unknown");

            await _webServer.PublishStatusAsync("hmr-fallback");
            await _webServer.UpdateClientsAsync();
        }
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

    private async Task InspectBackendManifestAsync(AppWorkspace workspace, CancellationToken cancellationToken)
    {
        try
        {
            ModuleBuildManifest manifest = await BackendManifestLoader.LoadAsync(workspace, cancellationToken);
            if (manifest.Module is not { } module)
            {
                _logger.LogDebug("Backend manifest loaded without module metadata.");
                return;
            }

            if (module.Capabilities is { Count: > 0 } capabilities)
            {
                _logger.LogDebug(
                    "Backend capabilities: {Capabilities}.",
                    string.Join(", ", capabilities));
            }

            if (module.Routes is { Count: > 0 } routes)
            {
                string routeList = string.Join(
                    ", ",
                    routes.Select(route => $"{route.Method.ToUpperInvariant()} {route.Path}"));

                _logger.LogDebug(
                    "Backend routes available ({RouteCount}): {Routes}.",
                    routes.Count,
                    routeList);
            }
            else
            {
                _logger.LogDebug("Backend manifest loaded; no route entries defined.");
            }
        }
        catch (FileNotFoundException)
        {
            _logger.LogDebug(
                "Backend manifest not found at {ManifestPath}; skipping route inspection.",
                workspace.BackendManifestPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to inspect backend manifest at {ManifestPath}.",
                workspace.BackendManifestPath);
        }
    }
}
