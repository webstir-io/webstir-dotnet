using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Engine.Services;

public enum FileChangeType
{
    Modified,
    Created,
    Deleted,
    Renamed
}

public record FileChangeEvent(
    string FilePath,
    FileChangeType ChangeType,
    DateTime Timestamp
);

public class ChangeService(ILogger<ChangeService> logger)
{
    private readonly ILogger<ChangeService> _logger = logger;

    private static readonly string[] IgnoredFiles = ["Thumbs.db", ".DS_Store"];
    private static readonly string[] IgnoredExtensions = [".tmp"];

    private readonly Channel<FileChangeEvent> _channel = Channel.CreateUnbounded<FileChangeEvent>();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _processingTask;

    private Func<string?, bool, Task>? _onChangeAction;
    private Func<AppWorkspace, Task>? _onServerRestart;
    private Func<Task>? _onClientNotification;
    private AppWorkspace? _workspace;

    public async Task Initialize(AppWorkspace workspace, Func<string?, bool, Task>? onChangeAction = null,
        Func<AppWorkspace, Task>? onServerRestart = null, Func<Task>? onClientNotification = null)
    {
        _workspace = workspace;
        _onChangeAction = onChangeAction;
        _onServerRestart = onServerRestart;
        _onClientNotification = onClientNotification;

        await Task.CompletedTask;
    }

    public void EnqueueChange(string filePath, FileChangeType changeType)
    {
        if (IsIgnored(filePath))
        {
            _logger.LogDebug("Ignoring file change: {FilePath}", filePath);
            return;
        }

        FileChangeEvent changeEvent = new(filePath, changeType, DateTime.UtcNow);

        if (!_channel.Writer.TryWrite(changeEvent))
        {
            _logger.LogWarning("Failed to enqueue file change: {FilePath}", filePath);
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
                _logger.LogInformation("File change detected: {FilePath} ({ChangeType})",
                    changeEvent.FilePath, changeEvent.ChangeType);

                switch (changeEvent.ChangeType)
                {
                    case FileChangeType.Modified:
                    case FileChangeType.Created:
                    case FileChangeType.Renamed:
                        await WaitForFileAsync(changeEvent.FilePath);
                        await _onChangeAction?.Invoke(changeEvent.FilePath, false)!;

                        if (IsServerFile(changeEvent.FilePath))
                        {
                            _logger.LogInformation("Backend files changed, requesting server restart...");
                            if (_onServerRestart != null)
                            {
                                await _onServerRestart(_workspace!);
                            }
                        }

                        if (_onClientNotification != null)
                        {
                            await _onClientNotification();
                        }
                        break;

                    case FileChangeType.Deleted:
                        _logger.LogInformation("File deleted: {FileName}", Path.GetFileName(changeEvent.FilePath));
                        await _onChangeAction?.Invoke(changeEvent.FilePath, false)!;

                        if (_onClientNotification != null)
                        {
                            await _onClientNotification();
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

    private static bool IsIgnored(string filePath)
    {
        string fileName = Path.GetFileName(filePath);

        return fileName.StartsWith('.')
               || fileName.EndsWith('~')
               || IgnoredFiles.Contains(fileName)
               || IgnoredExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.Ordinal));
    }
}
