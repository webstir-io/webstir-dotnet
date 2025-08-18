using Engine.Servers;

namespace Engine.Services;

public class WatchService(IWebServer _webServer, INodeServer _nodeServer)
{
    private static readonly string[] IgnoredFiles = ["Thumbs.db", ".DS_Store"];
    private static readonly string[] IgnoredExtensions = [".tmp"];
    
    private Func<bool, Task>? _onChangeAction;
    private AppContext? _context;
    private DateTime _lastChangeTime = DateTime.MinValue;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(200);

    public async Task Watch(AppContext context, Func<bool, Task>? onChangeAction = null)
    {
        _context = context;
        _onChangeAction = onChangeAction;
        await StartFileWatching(context);
    }

    private async Task StartFileWatching(AppContext context)
    {
        Console.WriteLine("Watching for changes...");

        using var watcher = CreateFileSystemWatcher(context);
        
        try
        {
            await StartServers(context);
            await WaitForExit();
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
            Console.WriteLine("Stopped watching");
        }
    }

    private FileSystemWatcher CreateFileSystemWatcher(AppContext context)
    {
        var watcher = new FileSystemWatcher(context.SrcPath)
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

    private async Task StartServers(AppContext context)
    {
        await _webServer.StartAsync(context);
        await _nodeServer.StartAsync(context);
    }

    private async Task WaitForExit()
    {
        Console.WriteLine("Press Ctrl+C to exit.");
        
        var exitEvent = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += async (sender, e) =>
        {
            e.Cancel = true;
            try
            {
                await StopServersAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping servers during exit: {ex.Message}");
            }
            exitEvent.SetResult(true);
        };
        
        await exitEvent.Task;
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
                await _nodeServer.RestartAsync();
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
        return filePath.StartsWith(_context!.ServerPath, StringComparison.OrdinalIgnoreCase);
    }
}
