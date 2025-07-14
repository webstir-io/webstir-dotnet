using Engine.Interfaces;

namespace Engine.Services;

public class WatchService(App _app, IWebServer _webServer, INodeServer _nodeServer)
{
    private static readonly string[] IgnoredFiles = ["Thumbs.db", ".DS_Store"];
    private static readonly string[] IgnoredExtensions = [".tmp"];
    
    private Action<bool>? _onChangeAction;
    private DateTime _lastChangeTime = DateTime.MinValue;
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(200);

    public async Task Watch(Action<bool> onChangeAction)
    {
        Console.WriteLine("Watching for changes...");

        using var watcher = new FileSystemWatcher(_app.SrcDir.FullName);
        _onChangeAction = onChangeAction;

        watcher.NotifyFilter = NotifyFilters.CreationTime
            | NotifyFilters.DirectoryName
            | NotifyFilters.FileName
            | NotifyFilters.LastWrite;

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnChanged;
        watcher.Error += OnError;
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
        
        // Start both servers
        await _webServer.StartAsync();
        await _nodeServer.StartAsync();

        Console.WriteLine("Press Ctrl+C to exit.");
        
        // Set up Ctrl+C handler
        var exitEvent = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += async (sender, e) =>
        {
            e.Cancel = true; // Prevent immediate termination
            await StopServersAsync();
            exitEvent.SetResult(true);
        };
        
        // Wait for Ctrl+C
        await exitEvent.Task;
        
        Console.WriteLine("Stopping servers...");
        await StopServersAsync();
        
        Console.WriteLine("Stopped watching");
    }

    private async Task StopServersAsync()
    {
        await Task.WhenAll(
            _webServer.StopAsync(),
            _nodeServer.StopAsync()
        );
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
            _onChangeAction!.Invoke(false);
            
            // If server files changed, restart Node.js server
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
            _onChangeAction!.Invoke(false);
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
        
        return fileName.StartsWith('.') || // Hidden files
               fileName.EndsWith('~') || // Backup files
               IgnoredFiles.Contains(fileName) ||
               IgnoredExtensions.Any(fileName.EndsWith);
    }

    private bool IsServerFile(string filePath)
    {
        return filePath.StartsWith(_app.ServerDir.FullName, StringComparison.OrdinalIgnoreCase);
    }
}
