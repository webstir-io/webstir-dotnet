using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Engine.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utilities.ProcessRunner;

namespace Engine.Servers;

public class NodeServer(IOptions<AppSettings> options, ILogger<NodeServer> logger, IProcessRunner processRunner)
{
    private readonly AppSettings _settings = options.Value;
    private readonly ILogger<NodeServer> _logger = logger;
    private readonly IProcessRunner _processRunner = processRunner;
    private IProcessHandle? _processHandle;

    public async Task StartAsync(AppWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        await KillProcessOnPortAsync(_settings.ApiServerPort, cancellationToken).ConfigureAwait(false);

        string serverIndexPath = workspace.BackendBuildPath.Combine("index.js");
        if (!File.Exists(serverIndexPath))
        {
            _logger.LogWarning("Backend build not found. Skipping Node.js server.");
            return;
        }

        ProcessSpec spec = new()
        {
            FileName = "node",
            Arguments = serverIndexPath,
            WorkingDirectory = workspace.WorkingPath,
            ReadySignal = "API server running",
            ReadySignalTimeout = TimeSpan.FromSeconds(30),
            TerminationMethod = TerminationMethod.Kill,
            OutputObserver = output =>
            {
                if (output.Stream == ProcessOutputStream.StandardError)
                {
                    if (!string.IsNullOrEmpty(output.Data))
                    {
                        _logger.LogError("Node server error: {ErrorData}", output.Data);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(output.Data) && !output.Data.Contains("SIGINT received", StringComparison.Ordinal))
                    {
                        _logger.LogInformation("{NodeOutput}", output.Data);
                    }
                }
            }
        };

        spec.WithEnvironmentVariable("NODE_ENV", "development");
        spec.WithEnvironmentVariable("PORT", _settings.ApiServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        spec.WithEnvironmentVariable("WEB_SERVER_URL", _settings.WebServerUrl);
        spec.WithEnvironmentVariable("API_SERVER_URL", _settings.ApiServerUrl);

        try
        {
            _processHandle = await _processRunner.StartAsync(spec, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Node server failed to report readiness within {Timeout} seconds.", spec.ReadySignalTimeout.TotalSeconds);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Node.js server.");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_processHandle is not null)
        {
            try
            {
                await _processHandle.StopAsync(TerminationMethod.Kill, cancellationToken).ConfigureAwait(false);
                await _processHandle.WaitForExitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error stopping Node.js server; attempting final disposal.");
            }
            finally
            {
                await _processHandle.DisposeAsync().ConfigureAwait(false);
                _processHandle = null;
            }
        }
    }

    private async Task KillProcessOnPortAsync(int port, CancellationToken cancellationToken = default)
    {
        try
        {
            string? pid = await GetProcessIdOnPortAsync(port, cancellationToken).ConfigureAwait(false);
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

            ProcessSpec spec = new()
            {
                FileName = command,
                Arguments = arguments,
                ExitTimeout = TimeSpan.FromSeconds(5)
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                spec.AllowExitCode(128); // process not found
            }
            else
            {
                spec.AllowExitCode(1); // process not found
            }

            await _processRunner.RunAsync(spec, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not kill process on port {Port}: {Message}", port, ex.Message);
        }
    }

    private async Task<string?> GetProcessIdOnPortAsync(int port, CancellationToken cancellationToken)
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

            ProcessSpec spec = new()
            {
                FileName = command,
                Arguments = arguments,
                ExitTimeout = TimeSpan.FromSeconds(5)
            };
            ProcessResult result = await _processRunner.RunAsync(spec, cancellationToken).ConfigureAwait(false);
            string output = result.StandardOutput;

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
