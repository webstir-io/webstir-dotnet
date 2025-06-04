using System.Collections.Concurrent;

namespace CLI;

public class Watcher(Server _server)
{
    private Action<bool>? _onChangeAction;

    public void Watch(Action<bool> onChangeAction)
    {
        Console.WriteLine("Watching for changes...");
        
        using var watcher = new FileSystemWatcher(Directories.SourceDirectory.FullName);
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
        
        _server.Start();        

        Console.WriteLine("Press enter to exit.");
        Console.ReadLine();
        Console.WriteLine("Stopped watching");
    }

    private async void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath.EndsWith(".DS_Store"))
            return;

        Console.WriteLine($"Detected file change: {e.FullPath}");
        WaitForFile(e.FullPath);
        _onChangeAction!.Invoke(false);
        await _server.Update();
    }

    private async void OnDeleted(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"File deleted");
        _onChangeAction!.Invoke(false);
        await _server.Update();
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

    private static void WaitForFile(string filePath, int timeoutMs = 10000, int checkIntervalMs = 500)
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
                Task.Delay(checkIntervalMs).Wait();
                timeElapsed += checkIntervalMs;
            }
        }

        Console.WriteLine("Warning: timeout waiting for file");
        return;
    }
}
