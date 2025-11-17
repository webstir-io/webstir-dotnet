using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Engine.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utilities.Process;

namespace Engine.Servers;

public class NodeServer(IOptions<AppSettings> options, ILogger<NodeServer> logger, IProcessRunner processRunner)
{
    private readonly AppSettings _settings = options.Value;
    private readonly ILogger<NodeServer> _logger = logger;
    private readonly IProcessRunner _processRunner = processRunner;
    private IProcessHandle? _processHandle;
    private readonly BackendRuntimeOptions _runtimeOptions = BackendRuntimeOptions.FromEnvironment(options.Value, logger);
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private const string WaitForReadyEnvironmentVariable = "WEBSTIR_BACKEND_WAIT_FOR_READY";
    private const string ReadyTimeoutEnvironmentVariable = "WEBSTIR_BACKEND_READY_TIMEOUT_SECONDS";
    private const string HealthCheckEnvironmentVariable = "WEBSTIR_BACKEND_HEALTHCHECK";
    private const string HealthPathEnvironmentVariable = "WEBSTIR_BACKEND_HEALTH_PATH";
    private const string HealthTimeoutEnvironmentVariable = "WEBSTIR_BACKEND_HEALTH_TIMEOUT_SECONDS";
    private const string HealthAttemptsEnvironmentVariable = "WEBSTIR_BACKEND_HEALTH_ATTEMPTS";
    private const string HealthDelayEnvironmentVariable = "WEBSTIR_BACKEND_HEALTH_DELAY_MILLISECONDS";
    private const string TerminationMethodEnvironmentVariable = "WEBSTIR_BACKEND_TERMINATION";

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
            ReadySignalTimeout = _runtimeOptions.ReadySignalTimeout,
            WaitForReadySignalOnStart = _runtimeOptions.WaitForReadySignal,
            TerminationMethod = _runtimeOptions.TerminationMethod,
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
        spec.WithEnvironmentVariable("PORT", _settings.ApiServerPort.ToString(CultureInfo.InvariantCulture));
        spec.WithEnvironmentVariable("WEB_SERVER_URL", _settings.WebServerUrl);
        spec.WithEnvironmentVariable("API_SERVER_URL", _settings.ApiServerUrl);
        if (_runtimeOptions.HealthProbeEnabled)
        {
            spec.WithEnvironmentVariable("WEBSTIR_BACKEND_HEALTHCHECK", "enabled");
        }

        if (_runtimeOptions.WaitForReadySignal)
        {
            _logger.LogDebug(
                "Waiting for backend ready signal '{ReadySignal}' (timeout {TimeoutSeconds}s).",
                spec.ReadySignal,
                _runtimeOptions.ReadySignalTimeout.TotalSeconds);
        }
        else
        {
            _logger.LogInformation(
                "Skipping backend ready signal wait via {EnvironmentVariable}.",
                WaitForReadyEnvironmentVariable);
        }

        if (_runtimeOptions.HealthProbeEnabled)
        {
            _logger.LogDebug(
                "Backend health probe enabled for {HealthUri} (timeout {TimeoutSeconds}s, attempts {Attempts}).",
                _runtimeOptions.HealthProbeUri,
                _runtimeOptions.HealthProbeTimeout.TotalSeconds,
                _runtimeOptions.HealthProbeAttempts);
        }
        else
        {
            _logger.LogInformation(
                "Backend health probe disabled via {EnvironmentVariable}.",
                HealthCheckEnvironmentVariable);
        }

        _logger.LogDebug(
            "Backend termination method: {TerminationMethod} (override {EnvVar}).",
            _runtimeOptions.TerminationMethod,
            TerminationMethodEnvironmentVariable);

        try
        {
            _processHandle = await _processRunner.StartAsync(spec, cancellationToken).ConfigureAwait(false);
            if (_runtimeOptions.HealthProbeEnabled)
            {
                await EnsureBackendHealthyAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Node server failed to report readiness within {Timeout} seconds.", spec.ReadySignalTimeout.TotalSeconds);
            await StopInternalAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Node.js server.");
            await StopInternalAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => StopInternalAsync(cancellationToken);

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

    private async Task EnsureBackendHealthyAsync(CancellationToken cancellationToken)
    {
        if (_processHandle is null)
        {
            return;
        }

        for (int attempt = 1; attempt <= _runtimeOptions.HealthProbeAttempts; attempt++)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(_runtimeOptions.HealthProbeTimeout);

            try
            {
                using HttpResponseMessage response = await HttpClient.GetAsync(_runtimeOptions.HealthProbeUri, linkedCts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Backend health probe succeeded (status {StatusCode}).",
                        (int)response.StatusCode);
                    return;
                }

                _logger.LogWarning(
                    "Backend health probe attempt {Attempt}/{Total} returned status {StatusCode}.",
                    attempt,
                    _runtimeOptions.HealthProbeAttempts,
                    (int)response.StatusCode);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Backend health probe attempt {Attempt}/{Total} timed out after {Timeout}.",
                    attempt,
                    _runtimeOptions.HealthProbeAttempts,
                    _runtimeOptions.HealthProbeTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Backend health probe attempt {Attempt}/{Total} failed.",
                    attempt,
                    _runtimeOptions.HealthProbeAttempts);
            }

            if (attempt < _runtimeOptions.HealthProbeAttempts)
            {
                await Task.Delay(_runtimeOptions.HealthProbeDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            $"Backend health probe failed after {_runtimeOptions.HealthProbeAttempts} attempts.");
    }

    private async Task StopInternalAsync(CancellationToken cancellationToken)
    {
        if (_processHandle is null)
        {
            return;
        }

        try
        {
            await _processHandle.StopAsync(_runtimeOptions.TerminationMethod, cancellationToken).ConfigureAwait(false);
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

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.ConnectionClose = false;
        return client;
    }

    private sealed class BackendRuntimeOptions
    {
        private BackendRuntimeOptions(
            bool waitForReadySignal,
            TimeSpan readySignalTimeout,
            bool healthProbeEnabled,
            Uri healthProbeUri,
            TimeSpan healthProbeTimeout,
            int healthProbeAttempts,
            TimeSpan healthProbeDelay,
            TerminationMethod terminationMethod)
        {
            WaitForReadySignal = waitForReadySignal;
            ReadySignalTimeout = readySignalTimeout;
            HealthProbeEnabled = healthProbeEnabled;
            HealthProbeUri = healthProbeUri;
            HealthProbeTimeout = healthProbeTimeout;
            HealthProbeAttempts = healthProbeAttempts;
            HealthProbeDelay = healthProbeDelay;
            TerminationMethod = terminationMethod;
        }

        public bool WaitForReadySignal
        {
            get;
        }

        public TimeSpan ReadySignalTimeout
        {
            get;
        }

        public bool HealthProbeEnabled
        {
            get;
        }

        public Uri HealthProbeUri
        {
            get;
        }

        public TimeSpan HealthProbeTimeout
        {
            get;
        }

        public int HealthProbeAttempts
        {
            get;
        }

        public TimeSpan HealthProbeDelay
        {
            get;
        }

        public TerminationMethod TerminationMethod
        {
            get;
        }

        public static BackendRuntimeOptions FromEnvironment(AppSettings settings, ILogger logger)
        {
            bool waitForReadySignal = !IsDisabled(Environment.GetEnvironmentVariable(WaitForReadyEnvironmentVariable));
            TimeSpan readyTimeout = ParseSeconds(Environment.GetEnvironmentVariable(ReadyTimeoutEnvironmentVariable), TimeSpan.FromSeconds(30), ReadyTimeoutEnvironmentVariable, logger);

            bool healthProbeEnabled = !IsDisabled(Environment.GetEnvironmentVariable(HealthCheckEnvironmentVariable));

            string? pathOverride = Environment.GetEnvironmentVariable(HealthPathEnvironmentVariable);
            Uri healthUri = BuildHealthUri(settings.ApiServerUrl, pathOverride, logger);

            TimeSpan healthTimeout = ParseSeconds(Environment.GetEnvironmentVariable(HealthTimeoutEnvironmentVariable), TimeSpan.FromSeconds(5), HealthTimeoutEnvironmentVariable, logger);
            int healthAttempts = ParseAttempts(Environment.GetEnvironmentVariable(HealthAttemptsEnvironmentVariable), 5, HealthAttemptsEnvironmentVariable, logger);
            TimeSpan healthDelay = ParseMilliseconds(Environment.GetEnvironmentVariable(HealthDelayEnvironmentVariable), TimeSpan.FromMilliseconds(250), HealthDelayEnvironmentVariable, logger);

            TerminationMethod method = ParseTerminationMethod(Environment.GetEnvironmentVariable(TerminationMethodEnvironmentVariable));

            BackendRuntimeOptions options = new BackendRuntimeOptions(
                waitForReadySignal,
                readyTimeout,
                healthProbeEnabled,
                healthUri,
                healthTimeout,
                healthAttempts,
                healthDelay,
                method);

            return options;
        }

        private static bool IsDisabled(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().ToLowerInvariant();
            return normalized is "0" or "false" or "off" or "skip" or "disabled";
        }

        private static TimeSpan ParseSeconds(string? value, TimeSpan fallback, string variableName, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds > 0)
            {
                return TimeSpan.FromSeconds(seconds);
            }

            logger.LogWarning(
                "Environment variable {Variable} has invalid value '{Value}'. Using fallback of {FallbackSeconds} seconds.",
                variableName,
                value,
                fallback.TotalSeconds);
            return fallback;
        }

        private static TimeSpan ParseMilliseconds(string? value, TimeSpan fallback, string variableName, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds) && milliseconds >= 0)
            {
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            logger.LogWarning(
                "Environment variable {Variable} has invalid value '{Value}'. Using fallback of {FallbackMilliseconds} milliseconds.",
                variableName,
                value,
                fallback.TotalMilliseconds);
            return fallback;
        }

        private static int ParseAttempts(string? value, int fallback, string variableName, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int attempts) && attempts > 0)
            {
                return attempts;
            }

            logger.LogWarning(
                "Environment variable {Variable} has invalid value '{Value}'. Using fallback of {FallbackAttempts}.",
                variableName,
                value,
                fallback);
            return fallback;
        }

        private static Uri BuildHealthUri(string baseUrl, string? pathOverride, ILogger logger)
        {
            string path = string.IsNullOrWhiteSpace(pathOverride)
                ? "/api/health"
                : pathOverride.Trim();

            if (!path.StartsWith('/'))
            {
                path = "/" + path;
            }

            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri))
            {
                return new Uri(baseUri, path);
            }

            logger.LogWarning(
                "Failed to parse API server URL '{BaseUrl}'. Falling back to http://localhost:8008{Path}.",
                baseUrl,
                path);
            return new Uri($"http://localhost:8008{path}");
        }

        private static TerminationMethod ParseTerminationMethod(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Utilities.Process.TerminationMethod.Kill;
            }

            string v = value.Trim().ToLowerInvariant();
            return v is "ctrlc" or "ctrl-c" or "int" ? Utilities.Process.TerminationMethod.CtrlC : Utilities.Process.TerminationMethod.Kill;
        }
    }
}
