using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Engine.Bridge.Frontend;

internal sealed class FrontendWatcher
{
    private readonly AppWorkspace _workspace;
    private readonly ILogger _logger;
    private readonly string _diagnosticPrefix;
    private readonly JsonSerializerOptions _diagnosticSerializerOptions;
    private readonly Action<FrontendCliDiagnostic> _diagnosticHandler;
    private readonly Action<string?, bool> _outputHandler;
    private readonly Func<string> _resolveExecutablePath;
    private readonly bool _verbose;

    private readonly Queue<TaskCompletionSource<FrontendCliDiagnostic>> _pendingCommands = new();
    private readonly object _pendingCommandsLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _setupLock = new(1, 1);

    private Process? _process;
    private StreamWriter? _input;
    private bool _ready;
    private bool _stopping;

    public FrontendWatcher(
        AppWorkspace workspace,
        ILogger logger,
        string diagnosticPrefix,
        JsonSerializerOptions diagnosticSerializerOptions,
        Action<FrontendCliDiagnostic> diagnosticHandler,
        Action<string?, bool> outputHandler,
        Func<string> resolveExecutablePath,
        bool verboseLogging = false)
    {
        _workspace = workspace;
        _logger = logger;
        _diagnosticPrefix = diagnosticPrefix;
        _diagnosticSerializerOptions = diagnosticSerializerOptions;
        _diagnosticHandler = diagnosticHandler;
        _outputHandler = outputHandler;
        _resolveExecutablePath = resolveExecutablePath;
        _verbose = verboseLogging;
    }

    public async Task StartAsync()
    {
        await _setupLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await EnsureProcessAsync().ConfigureAwait(false);
            if (_ready)
            {
                return;
            }

            await WritePayloadAsync(new { type = "start" }, waitForCompletion: true, CancellationToken.None).ConfigureAwait(false);
            _ready = true;
        }
        finally
        {
            _setupLock.Release();
        }
    }

    public async Task SendAsync(object payload, bool waitForCompletion, CancellationToken cancellationToken)
    {
        await StartAsync().ConfigureAwait(false);
        await WritePayloadAsync(payload, waitForCompletion, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        Process? process = _process;
        if (process is null)
        {
            _ready = false;
            ClearPendingCommands(new TaskCanceledException("Frontend watch daemon stopped."));
            return;
        }

        _stopping = true;

        try
        {
            if (_input is not null)
            {
                await WritePayloadAsync(new { type = "shutdown" }, waitForCompletion: false, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to send shutdown command to frontend watch daemon.");
        }

        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timed out waiting for frontend watch daemon to exit; terminating process.");
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception killEx) when (killEx is InvalidOperationException or NotSupportedException)
            {
                _logger.LogDebug(killEx, "Failed to terminate frontend watch daemon process after timeout.");
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        finally
        {
            _ready = false;
            CleanupProcess(resetReadyState: true);
            ClearPendingCommands(new TaskCanceledException("Frontend watch daemon stopped."));
            _stopping = false;
        }
    }

    private Task EnsureProcessAsync()
    {
        if (_process is { HasExited: false })
        {
            return Task.CompletedTask;
        }

        NodeRuntime.EnsureMinimumVersion();
        string executable = _resolveExecutablePath();
        if (!File.Exists(executable))
        {
            throw new InvalidOperationException($"webstir-frontend executable not found at {executable}. Run npm install to restore dependencies.");
        }

        ProcessStartInfo psi = new()
        {
            FileName = executable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _workspace.WorkingPath
        };

        psi.ArgumentList.Add("watch-daemon");
        psi.ArgumentList.Add("--workspace");
        psi.ArgumentList.Add(_workspace.WorkingPath);
        psi.ArgumentList.Add("--no-auto-start");
        if (_verbose)
        {
            psi.ArgumentList.Add("--verbose");
        }

        Process process = new()
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += OnProcessOutput;
        process.ErrorDataReceived += OnProcessError;
        process.Exited += OnProcessExited;

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start webstir-frontend watch daemon.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _process = process;
        _input = process.StandardInput;
        _input.AutoFlush = true;
        _ready = false;
        _stopping = false;

        return Task.CompletedTask;
    }

    private async Task WritePayloadAsync(object payload, bool waitForCompletion, CancellationToken cancellationToken)
    {
        if (_input is null)
        {
            throw new InvalidOperationException("Frontend watch daemon input stream is not available.");
        }

        TaskCompletionSource<FrontendCliDiagnostic>? completionSource = null;
        if (waitForCompletion)
        {
            completionSource = new TaskCompletionSource<FrontendCliDiagnostic>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        string serialized = JsonSerializer.Serialize(payload);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_input is null)
            {
                throw new InvalidOperationException("Frontend watch daemon input stream is not available.");
            }

            if (completionSource is not null)
            {
                EnqueueCompletion(completionSource);
            }

            await _input.WriteLineAsync(serialized).ConfigureAwait(false);
            await _input.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        if (completionSource is null)
        {
            return;
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
        await completionSource.Task.ConfigureAwait(false);
    }

    private void OnProcessOutput(object? sender, DataReceivedEventArgs args) => HandleProcessOutput(args.Data, isError: false);

    private void OnProcessError(object? sender, DataReceivedEventArgs args) => HandleProcessOutput(args.Data, isError: true);

    private void HandleProcessOutput(string? line, bool isError)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (TryParseDiagnostic(line, out FrontendCliDiagnostic? diagnostic))
        {
            _diagnosticHandler(diagnostic!);
            HandleDiagnostic(diagnostic!);
            return;
        }

        _outputHandler(line, isError);
    }

    private bool TryParseDiagnostic(string line, out FrontendCliDiagnostic? diagnostic)
    {
        if (!line.StartsWith(_diagnosticPrefix, StringComparison.Ordinal))
        {
            diagnostic = null;
            return false;
        }

        string json = line[_diagnosticPrefix.Length..];

        try
        {
            diagnostic = JsonSerializer.Deserialize<FrontendCliDiagnostic>(json, _diagnosticSerializerOptions);
            if (diagnostic is null)
            {
                return false;
            }

            return string.Equals(diagnostic.Type, "diagnostic", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            diagnostic = null;
            return false;
        }
    }

    private void HandleDiagnostic(FrontendCliDiagnostic diagnostic)
    {
        switch (diagnostic.Code)
        {
            case "frontend.watch.pipeline.success":
                CompletePendingCommandSuccess(diagnostic);
                return;
            case "frontend.watch.javascript.build.failure":
            case "frontend.watch.command.failure":
            case "frontend.watch.unexpected":
                CompletePendingCommandFailure(CreateWatchCommandException(diagnostic));
                return;
        }

        if (string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase))
        {
            CompletePendingCommandFailure(CreateWatchCommandException(diagnostic));
        }
    }

    private void OnProcessExited(object? sender, EventArgs args)
    {
        int exitCode = sender is Process exitedProcess ? exitedProcess.ExitCode : -1;

        if (_stopping)
        {
            _logger.LogInformation("Frontend watch daemon exited with code {ExitCode}.", exitCode);
        }
        else
        {
            _logger.LogWarning("Frontend watch daemon exited unexpectedly with code {ExitCode}.", exitCode);
            InvalidOperationException exception = new("Frontend watch daemon exited unexpectedly.");
            CompletePendingCommandFailure(exception);
            ClearPendingCommands(exception);
        }

        _ready = false;
        CleanupProcess(resetReadyState: _stopping);
        _stopping = false;
    }

    private void CleanupProcess(bool resetReadyState)
    {
        Process? process = _process;
        if (process is not null)
        {
            process.OutputDataReceived -= OnProcessOutput;
            process.ErrorDataReceived -= OnProcessError;
            process.Exited -= OnProcessExited;
            process.Dispose();
        }

        _process = null;

        _input?.Dispose();
        _input = null;

        if (resetReadyState)
        {
            _ready = false;
        }
    }

    private void EnqueueCompletion(TaskCompletionSource<FrontendCliDiagnostic> completion)
    {
        lock (_pendingCommandsLock)
        {
            _pendingCommands.Enqueue(completion);
        }
    }

    private void CompletePendingCommandSuccess(FrontendCliDiagnostic diagnostic)
    {
        TaskCompletionSource<FrontendCliDiagnostic>? completion = DequeueCompletion();
        completion?.TrySetResult(diagnostic);
    }

    private void CompletePendingCommandFailure(Exception exception)
    {
        TaskCompletionSource<FrontendCliDiagnostic>? completion = DequeueCompletion();
        if (completion is null)
        {
            _logger.LogDebug("Received frontend watch diagnostic with no pending command. Error: {Message}", exception.Message);
            return;
        }

        completion.TrySetException(exception);
    }

    private TaskCompletionSource<FrontendCliDiagnostic>? DequeueCompletion()
    {
        lock (_pendingCommandsLock)
        {
            if (_pendingCommands.Count == 0)
            {
                return null;
            }

            return _pendingCommands.Dequeue();
        }
    }

    private void ClearPendingCommands(Exception exception)
    {
        TaskCompletionSource<FrontendCliDiagnostic>[] pending;
        lock (_pendingCommandsLock)
        {
            if (_pendingCommands.Count == 0)
            {
                return;
            }

            pending = _pendingCommands.ToArray();
            _pendingCommands.Clear();
        }

        foreach (TaskCompletionSource<FrontendCliDiagnostic> completion in pending)
        {
            completion.TrySetException(exception);
        }
    }

    private InvalidOperationException CreateWatchCommandException(FrontendCliDiagnostic diagnostic)
    {
        string message = string.IsNullOrWhiteSpace(diagnostic.Message)
            ? "Frontend watch command failed."
            : diagnostic.Message;

        if (diagnostic.Data is { Count: > 0 })
        {
            string serialized = JsonSerializer.Serialize(diagnostic.Data, _diagnosticSerializerOptions);
            message = $"{message} | data: {serialized}";
        }

        return new InvalidOperationException(message);
    }
}
