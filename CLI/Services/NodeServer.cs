using System.Diagnostics;
using CLI.Interfaces;

namespace CLI.Services;

public class NodeServer : INodeServer, IDisposable
{
    private Process? _nodeProcess;
    private readonly SemaphoreSlim _processLock = new(1, 1);
    
    public bool IsRunning => _nodeProcess != null && !_nodeProcess.HasExited;
    public event EventHandler<string>? OutputReceived;

    public async Task StartAsync()
    {
        await _processLock.WaitAsync();
        try
        {
            if (IsRunning)
            {
                Console.WriteLine("Node.js server is already running");
                return;
            }

            if (!Directory.Exists("build/server") || !File.Exists("build/server/index.js"))
            {
                Console.WriteLine("Server build not found. Skipping Node.js server startup.");
                return;
            }

            _nodeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "build/server/index.js",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                }
            };

            _nodeProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"[Node] {e.Data}");
                    OutputReceived?.Invoke(this, e.Data);
                }
            };

            _nodeProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"[Node Error] {e.Data}");
                }
            };

            _nodeProcess.Start();
            _nodeProcess.BeginOutputReadLine();
            _nodeProcess.BeginErrorReadLine();

            // Wait a bit for the server to start
            await Task.Delay(500);
            
            if (!IsRunning)
            {
                throw new InvalidOperationException("Node.js server failed to start");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start Node.js server: {ex.Message}");
            _nodeProcess?.Dispose();
            _nodeProcess = null;
        }
        finally
        {
            _processLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _processLock.WaitAsync();
        try
        {
            if (_nodeProcess == null || _nodeProcess.HasExited)
            {
                return;
            }

            Console.WriteLine("Stopping Node.js server...");
            
            // Try graceful shutdown first
            _nodeProcess.Kill(entireProcessTree: true);
            
            await _nodeProcess.WaitForExitAsync();
            _nodeProcess.Dispose();
            _nodeProcess = null;
            
            Console.WriteLine("Node.js server stopped");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping Node.js server: {ex.Message}");
        }
        finally
        {
            _processLock.Release();
        }
    }

    public async Task RestartAsync()
    {
        Console.WriteLine("Restarting Node.js server...");
        await StopAsync();
        await StartAsync();
    }

    public void Dispose()
    {
        StopAsync().Wait(5000);
        _processLock.Dispose();
        GC.SuppressFinalize(this);
    }
}