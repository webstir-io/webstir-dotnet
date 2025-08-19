using System.Diagnostics;
using System.Runtime.InteropServices;
using Engine.Extensions;
using Microsoft.Extensions.Options;

namespace Engine.Servers;

public class NodeServer(IOptions<AppSettings> options)
{
    private readonly AppSettings _settings = options.Value;
    private Process? _process;
    
    public async Task StartAsync(AppWorkspace workspace)
    {
        await KillProcessOnPort(_settings.ApiServerPort);
        
        var serverIndexPath = workspace.ServerBuildPath.Combine("index.js");        
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
                WorkingDirectory = workspace.WorkingPath
            }
        };
        
        _process.StartInfo.Environment["NODE_ENV"] = "development";
        _process.StartInfo.Environment["PORT"] = _settings.ApiServerPort.ToString();
        _process.StartInfo.Environment["WEB_SERVER_URL"] = _settings.WebServerUrl;
        
        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                if (!e.Data.Contains("SIGINT received"))
                    Console.WriteLine(e.Data);
                
                if (e.Data.Contains("API server running"))
                    startupComplete.TrySetResult(true);
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
    
    private static async Task KillProcessOnPort(int port)
    {
        try
        {
            var pid = await GetProcessIdOnPort(port);
            if (pid == null)
                return;
            
            string command;
            string arguments;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                command = "taskkill";
                arguments = $"/F /PID {pid}";
            }
            else
            {
                command = "kill";
                arguments = $"-9 {pid}";
            }
            
            using var killProcess = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            
            if (killProcess != null)
            {
                await killProcess.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not kill process on port {port}: {ex.Message}");
        }
    }
    
    private static async Task<string?> GetProcessIdOnPort(int port)
    {
        try
        {
            string command;
            string arguments;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                command = "cmd.exe";
                arguments = $"/c netstat -ano | findstr :{port}";
            }
            else
            {
                command = "lsof";
                arguments = $"-ti:{port}";
            }
            
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            
            if (process == null) return null;
            
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            if (string.IsNullOrWhiteSpace(output)) return null;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains($":{port}") && line.Contains("LISTENING"))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                            return parts[^1].Trim();
                    }
                }
            }
            else
            {
                return output.Trim().Split('\n')[0];
            }
            
            return null;
        }
        catch
        {
            // This is expected if port is free or lsof/netstat isn't available
            return null;
        }
    }
}