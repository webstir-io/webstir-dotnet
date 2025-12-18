using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Engine.Models;
using Microsoft.Extensions.Logging;

namespace Engine.Services;

public class ChangeService(ILogger<ChangeService> logger)
{
    private readonly ILogger<ChangeService> _logger = logger;

    private static readonly string[] IgnoredFiles = ["Thumbs.db", ".DS_Store"];
    private static readonly string[] IgnoredExtensions = [".tmp"];

    private readonly Channel<FileChangeEvent> _channel = Channel.CreateUnbounded<FileChangeEvent>();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _processingTask;

    private Func<string?, bool, Task<ChangeProcessingResult>>? _onChangeAction;
    private Func<AppWorkspace, Task>? _onServerRestart;
    private Func<ClientNotificationType, Task>? _onClientNotification;
    private Func<FrontendHotUpdate, Task>? _onHotUpdate;
    private AppWorkspace? _workspace;

    public async Task Initialize(
        AppWorkspace workspace,
        Func<string?, bool, Task<ChangeProcessingResult>>? onChangeAction = null,
        Func<AppWorkspace, Task>? onServerRestart = null,
        Func<ClientNotificationType, Task>? onClientNotification = null,
        Func<FrontendHotUpdate, Task>? onHotUpdate = null)
    {
        _workspace = workspace;
        _onChangeAction = onChangeAction;
        _onServerRestart = onServerRestart;
        _onClientNotification = onClientNotification;
        _onHotUpdate = onHotUpdate;

        await Task.CompletedTask;
    }

    public void EnqueueChange(string filePath, FileChangeType changeType)
    {
        string displayPath = _workspace?.ToDisplayPath(filePath) ?? filePath;
        if (IsIgnored(filePath))
        {
            _logger.LogDebug("Ignoring file change: {FilePath}", displayPath);
            return;
        }

        FileChangeEvent changeEvent = new(filePath, changeType, DateTime.UtcNow);

        if (!_channel.Writer.TryWrite(changeEvent))
        {
            _logger.LogWarning("Failed to enqueue file change: {FilePath}", displayPath);
        }
    }

    public Task StartAsync()
    {
        _processingTask = ProcessChangesAsync(_cancellationTokenSource.Token);
        return Task.CompletedTask;
    }

    private async Task ProcessChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (FileChangeEvent changeEvent in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                string displayPath = _workspace?.ToDisplayPath(changeEvent.FilePath) ?? changeEvent.FilePath;
                _logger.LogInformation("File change detected: {FilePath} ({ChangeType})",
                    displayPath, changeEvent.ChangeType);

                switch (changeEvent.ChangeType)
                {
                    case FileChangeType.Modified:
                    case FileChangeType.Created:
                    case FileChangeType.Renamed:
                        await WaitForFileAsync(changeEvent.FilePath);
                        await NotifyClientsAsync(ClientNotificationType.BuildStarting);

                        bool buildSucceeded = true;
                        ChangeProcessingResult changeResult = ChangeProcessingResult.Empty;

                        try
                        {
                            if (_onChangeAction is not null)
                            {
                                changeResult = await _onChangeAction.Invoke(changeEvent.FilePath, false);
                            }
                        }
                        catch (Exception ex)
                        {
                            buildSucceeded = false;
                            _logger.LogError(ex, "Frontend change processing failed for {FilePath}", displayPath);
                            await NotifyClientsAsync(ClientNotificationType.BuildFailed);
                        }

                        if (buildSucceeded && IsServerFile(changeEvent.FilePath))
                        {
                            _logger.LogInformation("Backend files changed, requesting server restart...");
                            if (_onServerRestart != null)
                            {
                                await _onServerRestart(_workspace!);
                            }
                        }

                        if (buildSucceeded)
                        {
                            await NotifyClientsAsync(ClientNotificationType.BuildSucceeded);
                            await DispatchClientNotificationAsync(changeResult);
                        }
                        break;

                    case FileChangeType.Deleted:
                        _logger.LogInformation("File deleted: {FileName}", Path.GetFileName(changeEvent.FilePath));
                        await NotifyClientsAsync(ClientNotificationType.BuildStarting);

                        bool deleteSucceeded = true;
                        ChangeProcessingResult deleteResult = ChangeProcessingResult.Empty;
                        try
                        {
                            if (_onChangeAction is not null)
                            {
                                deleteResult = await _onChangeAction.Invoke(changeEvent.FilePath, false);
                            }
                        }
                        catch (Exception ex)
                        {
                            deleteSucceeded = false;
                            _logger.LogError(ex, "Frontend deletion handling failed for {FilePath}", displayPath);
                            await NotifyClientsAsync(ClientNotificationType.BuildFailed);
                        }

                        if (deleteSucceeded)
                        {
                            await NotifyClientsAsync(ClientNotificationType.BuildSucceeded);
                            await DispatchClientNotificationAsync(deleteResult);
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Change processing stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background processing task failed");
            throw;
        }
    }

    public Task StopAsync() => StopInternalAsync();

    private Task StopInternalAsync()
    {
        _channel.Writer.Complete();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        return _processingTask ?? Task.CompletedTask;
    }


    private async Task WaitForFileAsync(string filePath, int timeoutMs = 10000, int checkIntervalMs = 500)
    {
        int timeElapsed = 0;
        while (timeElapsed < timeoutMs)
        {
            try
            {
                using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return;
            }
            catch (IOException)
            {
                await Task.Delay(checkIntervalMs);
                timeElapsed += checkIntervalMs;
            }
        }

        _logger.LogWarning("Timeout waiting for file to be ready");
    }

    private bool IsServerFile(string filePath) =>
        filePath.StartsWith(_workspace!.BackendPath, StringComparison.OrdinalIgnoreCase);

    private async Task NotifyClientsAsync(ClientNotificationType type)
    {
        if (_onClientNotification != null)
        {
            await _onClientNotification(type);
        }
    }

    private async Task DispatchClientNotificationAsync(ChangeProcessingResult result)
    {
        if (result.HotUpdate is { } hotUpdate)
        {
            if (!hotUpdate.RequiresReload)
            {
                await NotifyClientsAsync(ClientNotificationType.HotUpdate);
                await NotifyHotUpdateAsync(hotUpdate);
                return;
            }

            if (hotUpdate.FallbackReasons.Count > 0)
            {
                _logger.LogDebug(
                    "Hot update fallback requested for {ChangedFile}. Reasons: {FallbackReasons}",
                    hotUpdate.ChangedFile ?? "unknown",
                    string.Join(", ", hotUpdate.FallbackReasons));
            }
        }

        await NotifyClientsAsync(ClientNotificationType.Reload);
    }

    private async Task NotifyHotUpdateAsync(FrontendHotUpdate hotUpdate)
    {
        if (_onHotUpdate is null)
        {
            return;
        }

        await _onHotUpdate(hotUpdate);
    }

    private static bool IsIgnored(string filePath)
    {
        string fileName = Path.GetFileName(filePath);

        return fileName.StartsWith('.')
               || fileName.EndsWith('~')
               || IgnoredFiles.Contains(fileName)
               || IgnoredExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.Ordinal));
    }
}
