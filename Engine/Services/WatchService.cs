using Microsoft.Extensions.Logging;

namespace Engine.Services;

public class WatchService(ChangeService changeService, ILogger<WatchService> logger)
{
    private readonly ChangeService _changeService = changeService;
    private readonly ILogger<WatchService> _logger = logger;

    public async Task Watch(AppWorkspace workspace, Func<bool, Task>? onChangeAction = null)
    {
        await _changeService.Initialize(workspace, onChangeAction);
        await _changeService.StartAsync(CancellationToken.None);
        
        try
        {
            await StartFileWatching(workspace);
        }
        finally
        {
            await _changeService.StopAsync(CancellationToken.None);
        }
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
            _logger.LogInformation("Watching for file changes. Press Ctrl+C to exit.");
            await exitEvent.Task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file watching: {Message}", ex.Message);
            throw;
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
        watcher.Created += OnCreated;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnChanged;
        watcher.Error += OnError;

        return watcher;
    }


    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        _changeService.EnqueueChange(e.FullPath, FileChangeType.Changed);
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _changeService.EnqueueChange(e.FullPath, FileChangeType.Created);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        _changeService.EnqueueChange(e.FullPath, FileChangeType.Deleted);
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
}
