using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Engine.Models;
using Microsoft.Extensions.Logging;

namespace Engine.Services;

public class WatchService(ChangeService changeService, ILogger<WatchService> logger)
{
    private readonly ChangeService _changeService = changeService;
    private readonly ILogger<WatchService> _logger = logger;

    private FileSystemWatcher? _watcher;
    private readonly Dictionary<string, List<DateTime>> _pendingEvents = new(StringComparer.Ordinal);

    public Task Watch(AppWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
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
        _logger.LogInformation("Started watching for file changes in {SrcPath}", workspace.ToDisplayPath(workspace.SrcPath));
    }

    private FileSystemWatcher CreateFileSystemWatcher(AppWorkspace workspace)
    {
        FileSystemWatcher watcher = new(workspace.SrcPath)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnChanged;
        watcher.Created += OnCreated;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnError;

        return watcher;
    }


    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        FileInfo fileInfo = new(e.FullPath);
        if (!fileInfo.Exists)
        {
            return;
        }

        DateTime currentTimestamp = fileInfo.LastWriteTime;
        List<DateTime> pendingForFile = _pendingEvents.GetValueOrDefault(e.FullPath, []);

        if (!pendingForFile.Contains(currentTimestamp))
        {
            _changeService.EnqueueChange(e.FullPath, FileChangeType.Modified);
            pendingForFile.Clear();
            pendingForFile.Add(currentTimestamp);
            _pendingEvents[e.FullPath] = pendingForFile;
        }
    }

    private void OnCreated(object sender, FileSystemEventArgs e) =>
        _changeService.EnqueueChange(e.FullPath, FileChangeType.Created);

    private void OnDeleted(object sender, FileSystemEventArgs e) =>
        _changeService.EnqueueChange(e.FullPath, FileChangeType.Deleted);

    private void OnRenamed(object sender, RenamedEventArgs e) =>
        _changeService.EnqueueChange(e.FullPath, FileChangeType.Renamed);

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
