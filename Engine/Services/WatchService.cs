using Engine.Servers;

namespace Engine.Services;

public class WatchService(WebServer _webServer, NodeServer _nodeServer)
{
    private static readonly string[] IgnoredFiles = ["Thumbs.db", ".DS_Store"];
    private static readonly string[] IgnoredExtensions = [".tmp"];
    
    private Func<bool, Task>? _onChangeAction;
    private AppWorkspace? _workspace;
    private DateTime _lastChangeTime = DateTime.MinValue;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(200);

    public async Task Watch(AppWorkspace workspace, Func<bool, Task>? onChangeAction = null)
    {
        _workspace = workspace;
        _onChangeAction = onChangeAction;
        await StartFileWatching(workspace);
    }

    private async Task StartFileWatching(AppWorkspace workspace)
    {
        using var watcher = CreateFileSystemWatcher(workspace);
        
        // Set up exit handler BEFORE starting servers
        // This ensures our handler runs before ASP.NET's handler
        var exitEvent = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            exitEvent.SetResult(true);
        };
        
        try
        {
            await StartServers(workspace);
            Console.WriteLine("Watching for file changes. Press Ctrl+C to exit.");
            await exitEvent.Task;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during file watching: {ex.Message}");
            throw;
        }
        finally
        {
            Console.WriteLine("Stopping servers...");
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
            Console.WriteLine($"Error stopping servers: {ex.Message}");
        }
    }

    private async void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath))
            return;

        // Debounce rapid changes
        var now = DateTime.UtcNow;
        if (now - _lastChangeTime < _debounceInterval)
            return;

        _lastChangeTime = now;

        try
        {
            Console.WriteLine($"Detected file change: {e.FullPath}");
            await WaitForFileAsync(e.FullPath);
            await _onChangeAction!.Invoke(false);
            
            if (IsServerFile(e.FullPath))
            {
                Console.WriteLine("Server files changed, restarting Node.js server...");
                await _nodeServer.StopAsync();
                await _nodeServer.StartAsync(_workspace!);
            }
            
            await _webServer.UpdateClientsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling file change: {ex.Message}");
        }
    }

    private async void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnoreFile(e.FullPath))
            return;

        try
        {
            Console.WriteLine($"File deleted: {e.Name}");
            await _onChangeAction!.Invoke(false);
            await _webServer.UpdateClientsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling file deletion: {ex.Message}");
        }
    }

    private static void OnError(object sender, ErrorEventArgs e) =>
        PrintException(e.GetException());

    private static void PrintException(Exception? ex)
    {
        if (ex != null)
        {
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine("Stacktrace:");
            Console.WriteLine(ex.StackTrace);
            Console.WriteLine();
            PrintException(ex.InnerException);
        }
    }

    private static async Task WaitForFileAsync(string filePath, int timeoutMs = 10000, int checkIntervalMs = 500)
    {
        Console.Write("Waiting for file...");

        var timeElapsed = 0;
        while (timeElapsed < timeoutMs)
        {
            try
            {
                // Try to open the file with FileShare.None to check if it's still locked
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                Console.WriteLine("Done");
                return;
            }
            catch (IOException)
            {
                // The file is still locked, so we need to wait and try again
                await Task.Delay(checkIntervalMs);
                timeElapsed += checkIntervalMs;
            }
        }

        Console.WriteLine("Warning: timeout waiting for file");
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
