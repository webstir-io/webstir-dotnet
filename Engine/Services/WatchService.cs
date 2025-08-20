using Microsoft.Extensions.Logging;

namespace Engine.Services;

public class WatchService(ChangeService changeService, ILogger<WatchService> logger)
{
    private readonly ChangeService _changeService = changeService;
    private readonly ILogger<WatchService> _logger = logger;

    private FileSystemWatcher? _watcher;

    public Task Watch(AppWorkspace workspace)
    {
        StartFileWatching(workspace);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    private void StartFileWatching(AppWorkspace workspace)
    {
        _watcher = CreateFileSystemWatcher(workspace);        
        _logger.LogInformation("Started watching for file changes in {SrcPath}", workspace.SrcPath);
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
        _changeService.EnqueueChange(e.FullPath, FileChangeType.Modified);
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
