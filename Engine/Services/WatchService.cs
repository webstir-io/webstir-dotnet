using Engine.Servers;
using Microsoft.Extensions.Logging;

namespace Engine.Services;

public class WatchService(WebServer webServer, NodeServer nodeServer, ILogger<WatchService> logger)
{
    private readonly WebServer _webServer = webServer;
    private readonly NodeServer _nodeServer = nodeServer;
    private readonly ILogger<WatchService> _logger = logger;
    private static readonly string[] IgnoredFiles = ["Thumbs.db", ".DS_Store"];
    private static readonly string[] IgnoredExtensions = [".tmp"];
    
    private Func<bool, Task>? _onChangeAction;
    private AppWorkspace? _workspace;

    public async Task Watch(AppWorkspace workspace, Func<bool, Task>? onChangeAction = null)
    {
        _workspace = workspace;
        _onChangeAction = onChangeAction;
        await StartFileWatching(workspace);
    }

    private async Task StartFileWatching(AppWorkspace workspace)
    {
        using var watcher = CreateFileSystemWatcher(workspace);        
        var exitEvent = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            exitEvent.SetResult(true);
        };
        
        try
        {
            await StartServers(workspace);
            _logger.LogInformation("Watching for file changes. Press Ctrl+C to exit.");
            await exitEvent.Task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file watching: {Message}", ex.Message);
            throw;
        }
        finally
        {
            _logger.LogInformation("Stopping servers...");
            await StopServersAsync();
        }
    }

    private FileSystemWatcher CreateFileSystemWatcher(AppWorkspace workspace)
    {
        var watcher = new FileSystemWatcher(workspace.SrcPath)
        {
            NotifyFilter = NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastWrite,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnChanged;
        watcher.Error += OnError;

        return watcher;
    }

    private async Task StartServers(AppWorkspace workspace)
    {
        await _webServer.StartAsync(workspace);
        await _nodeServer.StartAsync(workspace);
    }


    private async Task StopServersAsync()
    {
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
    }

    private async void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath))
            return;


        try
        {
            _logger.LogInformation("Detected file change: {FullPath}", e.FullPath);
            await WaitForFileAsync(e.FullPath);
            await _onChangeAction!.Invoke(false);
            
            if (IsServerFile(e.FullPath))
            {
                _logger.LogInformation("Server files changed, restarting Node.js server...");
                await _nodeServer.StopAsync();
                await _nodeServer.StartAsync(_workspace!);
            }
            
            await _webServer.UpdateClientsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file change: {Message}", ex.Message);
        }
    }

    private async void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath))
            return;

        try
        {
            _logger.LogInformation("File deleted: {Name}", e.Name);
            await _onChangeAction!.Invoke(false);
            await _webServer.UpdateClientsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file deletion: {Message}", ex.Message);
        }
    }

    private void OnError(object sender, ErrorEventArgs e) =>
        LogException(e.GetException());

    private void LogException(Exception? ex)
    {
        if (ex != null)
        {
            _logger.LogError(ex, "FileSystemWatcher error: {Message}", ex.Message);
            LogException(ex.InnerException);
        }
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

    private static bool ShouldIgnoreFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        return fileName.StartsWith('.') ||
               fileName.EndsWith('~') ||
               IgnoredFiles.Contains(fileName) ||
               IgnoredExtensions.Any(fileName.EndsWith);
    }

    private bool IsServerFile(string filePath)
    {
        return filePath.StartsWith(_workspace!.ServerPath, StringComparison.OrdinalIgnoreCase);
    }
}
