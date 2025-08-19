using System.Diagnostics;
using Engine.Extensions;

namespace Engine.Servers;

public class NodeServer(AppSettings settings)
{
    private Process? _process;
    
    public async Task StartAsync(AppContext context)
    {
        var serverIndexPath = context.ServerBuildPath.Combine("index.js");
        
        if (!File.Exists(serverIndexPath))
        {
            Console.WriteLine("Server build not found. Skipping Node.js server.");
            return;
        }
        
        var startupComplete = new TaskCompletionSource<bool>();
        
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = serverIndexPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = context.WorkingPath
            }
        };
        
        _process.StartInfo.Environment["NODE_ENV"] = "development";
        _process.StartInfo.Environment["PORT"] = settings.ApiServerPort.ToString();
        
        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                // Filter out the SIGINT shutdown message
                if (!e.Data.Contains("SIGINT received"))
                {
                    Console.WriteLine(e.Data);
                }
                
                if (e.Data.Contains("API server running"))
                {
                    startupComplete.TrySetResult(true);
                }
            }
        };
        
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine($"Error: {e.Data}");
        };
        
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        
        await startupComplete.Task;
    }
    
    public async Task StopAsync()
    {
        if (_process != null && !_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
            _process.Dispose();
            _process = null;
        }
    }
}