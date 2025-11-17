using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Utilities.Process;

internal sealed class ProcessHandle : IProcessHandle
{
    private readonly System.Diagnostics.Process _process;
    private readonly ProcessSpec _spec;
    private readonly Action<ProcessOutput>? _observer;
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
    private readonly TaskCompletionSource<ProcessResult> _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _stdoutCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _stderrCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool>? _readySignalCompletion;
    private readonly Stopwatch _stopwatch = new();
    private readonly object _stateLock = new();
    private bool _readySignalReceived;
    private bool _readySignalTimedOut;
    private bool _timedOut;
    private bool _cancelled;
    private bool _disposed;

    internal ProcessHandle(System.Diagnostics.Process process, ProcessSpec spec)
    {
        _process = process;
        _spec = spec;
        _observer = spec.OutputObserver;
        _readySignalCompletion = spec.ReadySignal is null
            ? null
            : new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public int ProcessId => _process.Id;

    public bool HasExited => _process.HasExited;

    internal void Attach()
    {
        _process.OutputDataReceived += HandleOutputDataReceived;
        _process.ErrorDataReceived += HandleErrorDataReceived;
        _process.Exited += HandleProcessExited;
    }

    internal void NotifyStarted() => _stopwatch.Start();

    public async Task<bool> WaitForReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_readySignalCompletion is null)
        {
            lock (_stateLock)
            {
                _readySignalReceived = true;
            }

            return true;
        }

        try
        {
            bool received = await _readySignalCompletion.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            if (received)
            {
                lock (_stateLock)
                {
                    _readySignalReceived = true;
                }
            }

            return received;
        }
        catch (TimeoutException)
        {
            lock (_stateLock)
            {
                _readySignalTimedOut = true;
            }

            _readySignalCompletion.TrySetResult(false);

            return false;
        }
    }

    public async Task<ProcessResult> WaitForExitAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (timeout is { } exitTimeout)
            {
                return await _completionSource.Task.WaitAsync(exitTimeout, cancellationToken).ConfigureAwait(false);
            }

            return await _completionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            MarkTimedOut();
            await EnsureTerminatedAsync(_spec.TerminationMethod, CancellationToken.None).ConfigureAwait(false);
            return await _completionSource.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            MarkCancelled();
            await EnsureTerminatedAsync(TerminationMethod.Kill, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ProcessResult> StopAsync(TerminationMethod? terminationMethod = null, CancellationToken cancellationToken = default)
    {
        if (HasExited)
        {
            return await _completionSource.Task.ConfigureAwait(false);
        }

        TerminationMethod method = terminationMethod ?? _spec.TerminationMethod;
        await EnsureTerminatedAsync(method, cancellationToken).ConfigureAwait(false);
        return await _completionSource.Task.ConfigureAwait(false);
    }

    public Task SendStandardInputAsync(string value, CancellationToken cancellationToken = default)
    {
        if (!_process.StartInfo.RedirectStandardInput)
        {
            throw new InvalidOperationException("Standard input is not redirected.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _process.StandardInput.WriteLineAsync(value);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (!HasExited)
            {
                await EnsureTerminatedAsync(TerminationMethod.Kill, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
            // Ignore failures while disposing.
        }

        _process.OutputDataReceived -= HandleOutputDataReceived;
        _process.ErrorDataReceived -= HandleErrorDataReceived;
        _process.Exited -= HandleProcessExited;
        _process.Dispose();
    }

    private void HandleOutputDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data is null)
        {
            _stdoutCompleted.TrySetResult(true);
            return;
        }

        AppendLine(_stdout, e.Data);
        _observer?.Invoke(new ProcessOutput(ProcessOutputStream.StandardOutput, e.Data));
        CheckReadySignal(e.Data, ProcessOutputStream.StandardOutput);
    }

    private void HandleErrorDataReceived(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data is null)
        {
            _stderrCompleted.TrySetResult(true);
            return;
        }

        AppendLine(_stderr, e.Data);
        _observer?.Invoke(new ProcessOutput(ProcessOutputStream.StandardError, e.Data));
        CheckReadySignal(e.Data, ProcessOutputStream.StandardError);
    }

    private void HandleProcessExited(object? sender, EventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                try
                {
                    _process.WaitForExit();
                }
                catch
                {
                    // ignored
                }

                _stdoutCompleted.TrySetResult(true);
                _stderrCompleted.TrySetResult(true);
                await Task.WhenAll(_stdoutCompleted.Task, _stderrCompleted.Task).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            lock (_stateLock)
            {
                _stopwatch.Stop();
            }

            _readySignalCompletion?.TrySetResult(_readySignalReceived);

            ProcessResult result = new()
            {
                ExitCode = _process.ExitCode,
                StandardOutput = _stdout.ToString(),
                StandardError = _stderr.ToString(),
                Duration = _stopwatch.Elapsed,
                TimedOut = _timedOut,
                WasCancelled = _cancelled,
                IsExitCodeAccepted = _spec.AcceptableExitCodes.Contains(_process.ExitCode),
                ReadySignalReceived = _readySignalReceived,
                ReadySignalTimedOut = _readySignalTimedOut
            };

            _completionSource.TrySetResult(result);
        });
    }

    private void CheckReadySignal(string data, ProcessOutputStream stream)
    {
        if (_readySignalCompletion is null || _spec.ReadySignal is null)
        {
            return;
        }

        if (_readySignalCompletion.Task.IsCompleted)
        {
            return;
        }

        if (_spec.ReadySignalStream != stream)
        {
            return;
        }

        if (data.Contains(_spec.ReadySignal, StringComparison.Ordinal))
        {
            lock (_stateLock)
            {
                _readySignalReceived = true;
            }

            _readySignalCompletion.TrySetResult(true);
        }
    }

    private async Task EnsureTerminatedAsync(TerminationMethod method, CancellationToken cancellationToken)
    {
        if (HasExited)
        {
            return;
        }

        await SendTerminationSignalAsync(_process, method, cancellationToken).ConfigureAwait(false);

        try
        {
            await _completionSource.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            if (!HasExited)
            {
                await SendTerminationSignalAsync(_process, TerminationMethod.Kill, cancellationToken).ConfigureAwait(false);
                try
                {
                    await _completionSource.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    TryKillProcessTree();
                }
            }
        }
    }

    private static Task SendTerminationSignalAsync(System.Diagnostics.Process process, TerminationMethod method, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            // Use a softer signal for CtrlC: request termination without /F; fall back escalates later if needed.
            string args = method == TerminationMethod.CtrlC
                ? $"/T /PID {process.Id}"
                : $"/F /T /PID {process.Id}";
            return ExecuteHelperAsync("taskkill", args, cancellationToken);
        }

        // On Unix, target the process ID directly. Escalation to SIGKILL and process-tree kill happens in EnsureTerminatedAsync.
        string signal = method == TerminationMethod.CtrlC ? "-INT" : "-TERM";
        return ExecuteHelperAsync("kill", $"{signal} {process.Id}", cancellationToken);
    }

    private static async Task ExecuteHelperAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        try
        {
            using System.Diagnostics.Process? helper = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (helper is not null)
            {
                // Drain streams to avoid inherited console noise (e.g., "kill: No such process")
                _ = helper.StandardOutput.ReadToEndAsync(cancellationToken);
                _ = helper.StandardError.ReadToEndAsync(cancellationToken);
                await helper.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // Swallow helper failures; fall back to direct kill if needed.
        }
    }

    private void AppendLine(StringBuilder builder, string value)
    {
        lock (_stateLock)
        {
            builder.AppendLine(value);
        }
    }

    private void MarkTimedOut()
    {
        lock (_stateLock)
        {
            _timedOut = true;
        }
    }

    private void MarkCancelled()
    {
        lock (_stateLock)
        {
            _cancelled = true;
        }
    }

    private void TryKillProcessTree()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch
        {
            try
            {
                _process.Kill();
            }
            catch
            {
                // final fallback ignored
            }
        }
    }
}
