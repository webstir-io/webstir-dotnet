using System.Threading.Channels;
using Engine.Servers;
using Microsoft.Extensions.Logging;

namespace Engine.Services;

public enum FileChangeType
{
    Changed,
    Created,
    Deleted,
    Renamed
}

public record FileChangeEvent(
    string FilePath,
    FileChangeType ChangeType,
    DateTime Timestamp
);

public class ChangeService(
    NodeServer nodeServer,
    WebServer webServer,
    ILogger<ChangeService> logger)
{
    private readonly NodeServer _nodeServer = nodeServer;
    private readonly WebServer _webServer = webServer;
    private readonly ILogger<ChangeService> _logger = logger;
    
    private static readonly string[] IgnoredFiles = ["Thumbs.db", ".DS_Store"];
    private static readonly string[] IgnoredExtensions = [".tmp"];

    private readonly Channel<FileChangeEvent> _channel = Channel.CreateUnbounded<FileChangeEvent>();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _processingTask;

    private Func<bool, Task>? _onChangeAction;
    private AppWorkspace? _workspace;

    public async Task Initialize(AppWorkspace workspace, Func<bool, Task>? onChangeAction = null)
    {
        _workspace = workspace;
        _onChangeAction = onChangeAction;
        
        // Start the servers
        await _webServer.StartAsync(workspace);
        await _nodeServer.StartAsync(workspace);
    }

    public void EnqueueChange(string filePath, FileChangeType changeType)
    {
        if (IsIgnored(filePath))
            return;
            
        var changeEvent = new FileChangeEvent(filePath, changeType, DateTime.UtcNow);        
        if (!_channel.Writer.TryWrite(changeEvent))
            _logger.LogWarning("Failed to enqueue file change: {FilePath}", filePath);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _processingTask = ProcessChangesAsync(_cancellationTokenSource.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.Complete();
        _cancellationTokenSource.Cancel();

        if (_processingTask != null)
            await _processingTask;

        try
        {
            await Task.WhenAll(
                _webServer.StopAsync(),
                _nodeServer.StopAsync()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping servers: {Message}", ex.Message);
        }

        _cancellationTokenSource.Dispose();
    }

    private async Task ProcessChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var changeEvent in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                _logger.LogInformation("Processing change event from queue: {FilePath}", changeEvent.FilePath);                
                await ProcessChangeEventAsync(changeEvent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background processing task failed");
            throw;
        }        
    }

    private async Task ProcessChangeEventAsync(FileChangeEvent changeEvent)
    {
        switch (changeEvent.ChangeType)
        {
            case FileChangeType.Changed:
            case FileChangeType.Created:
            case FileChangeType.Renamed:
                await HandleFileModification(changeEvent.FilePath);
                break;

            case FileChangeType.Deleted:
                await HandleFileDeletion(changeEvent.FilePath);
                break;
        }
    }

    private async Task HandleFileModification(string filePath)
    {
        await WaitForFileAsync(filePath);
        await _onChangeAction?.Invoke(false)!;

        if (IsServerFile(filePath))
        {
            _logger.LogInformation("Server files changed, restarting Node.js server...");
            await _nodeServer.StopAsync();
            await _nodeServer.StartAsync(_workspace!);
        }

        await _webServer.UpdateClientsAsync();
    }

    private async Task HandleFileDeletion(string filePath)
    {
        _logger.LogInformation("File deleted: {FileName}", Path.GetFileName(filePath));
        await _onChangeAction?.Invoke(false)!;
        await _webServer.UpdateClientsAsync();
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