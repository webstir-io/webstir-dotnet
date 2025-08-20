using System.Threading.Channels;
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

    private Func<bool, Task>? _onChangeAction;
    private Func<AppWorkspace, Task>? _onServerRestart;
    private Func<Task>? _onClientNotification;
    private AppWorkspace? _workspace;

    public async Task Initialize(AppWorkspace workspace, Func<bool, Task>? onChangeAction = null, 
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
            
        var changeEvent = new FileChangeEvent(filePath, changeType, DateTime.UtcNow);
        
        if (!_channel.Writer.TryWrite(changeEvent))
            _logger.LogWarning("Failed to enqueue file change: {FilePath}", filePath);
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
            await foreach (var changeEvent in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                _logger.LogInformation("File change detected: {FilePath} ({ChangeType})", 
                    changeEvent.FilePath, changeEvent.ChangeType);

                switch (changeEvent.ChangeType)
                {
                    case FileChangeType.Modified:
                    case FileChangeType.Created:
                    case FileChangeType.Renamed:
                        await WaitForFileAsync(changeEvent.FilePath);
                        await _onChangeAction?.Invoke(false)!;

                        if (IsServerFile(changeEvent.FilePath))
                        {
                            _logger.LogInformation("Server files changed, requesting server restart...");
                            if (_onServerRestart != null)
                                await _onServerRestart(_workspace!);
                        }

                        if (_onClientNotification != null)
                            await _onClientNotification();
                        break;

                    case FileChangeType.Deleted:
                        _logger.LogInformation("File deleted: {FileName}", Path.GetFileName(changeEvent.FilePath));
                        await _onChangeAction?.Invoke(false)!;
                        
                        if (_onClientNotification != null)
                            await _onClientNotification();
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

    public async Task StopAsync()
    {
        _channel.Writer.Complete();
        _cancellationTokenSource.Cancel();

        if (_processingTask != null)
            await _processingTask;

        _cancellationTokenSource.Dispose();
    }


    private async Task WaitForFileAsync(string filePath, int timeoutMs = 10000, int checkIntervalMs = 500)
    {
        var timeElapsed = 0;
        while (timeElapsed < timeoutMs)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
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

    private bool IsServerFile(string filePath)
    {
        return filePath.StartsWith(_workspace!.ServerPath, StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool IsIgnored(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        return fileName.StartsWith('.') ||
               fileName.EndsWith('~') ||
               IgnoredFiles.Contains(fileName) ||
               IgnoredExtensions.Any(fileName.EndsWith);
    }
}