using System.Diagnostics;
using System.Runtime.InteropServices;
using Engine.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Engine.Servers;

public class NodeServer(IOptions<AppSettings> options, ILogger<NodeServer> logger)
{
    private readonly AppSettings _settings = options.Value;
    private readonly ILogger<NodeServer> _logger = logger;
    private Process? _process;
    
    public async Task StartAsync(AppWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        await KillProcessOnPort(_settings.ApiServerPort);
        
        string serverIndexPath = workspace.ServerBuildPath.Combine("index.js");        
        if (!File.Exists(serverIndexPath))
        {
            _logger.LogWarning("Server build not found. Skipping Node.js server.");
            return;
        }
        
        TaskCompletionSource<bool> startupComplete = new();
        
        _process = new()
        {
            StartInfo = new()
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
        _process.StartInfo.Environment["API_SERVER_URL"] = _settings.ApiServerUrl;
        
        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                if (!e.Data.Contains("SIGINT received", StringComparison.Ordinal))
                {
                    _logger.LogInformation("{NodeOutput}", e.Data);
                }
                
                if (e.Data.Contains("API server running", StringComparison.Ordinal))
                {
                    startupComplete.TrySetResult(true);
                }
            }
        };
        
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _logger.LogError("Node server error: {ErrorData}", e.Data);
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
    
    private async Task KillProcessOnPort(int port)
    {
        try
        {
            string? pid = await GetProcessIdOnPort(port);
            if (pid == null)
            {
                return;
            }
            
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
            
            using Process? killProcess = Process.Start(new ProcessStartInfo
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
            _logger.LogWarning("Could not kill process on port {Port}: {Message}", port, ex.Message);
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
            
            ProcessStartInfo psi = new()
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using Process? process = Process.Start(psi);
            
            if (process == null)
            {
                return null;
            }
            
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string[] lines = output.Split('\n');
                foreach (string line in lines)
                {
                    if (line.Contains($":{port}", StringComparison.Ordinal) && line.Contains("LISTENING", StringComparison.Ordinal))
                    {
                        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5)
                        {
                            return parts[^1].Trim();
                        }
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
