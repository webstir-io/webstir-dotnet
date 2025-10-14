namespace Framework.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

internal interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}

internal sealed record ProcessRequest(
    string FileName,
    string Arguments,
    string WorkingDirectory,
    TimeSpan? Timeout = null,
    IReadOnlyDictionary<string, string>? EnvironmentVariables = null,
    string? DisplayName = null,
    bool StreamOutput = false);

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

internal sealed class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger = logger;

    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);

        ProcessStartInfo startInfo = new()
        {
            FileName = request.FileName,
            Arguments = request.Arguments,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (request.EnvironmentVariables is not null)
        {
            foreach ((string key, string value) in request.EnvironmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        string displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? $"{request.FileName} {request.Arguments}".Trim()
            : request.DisplayName!;

        _logger.LogDebug("Executing process: {Command}", displayName);

        using Process process = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process '{displayName}'.");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException($"Unable to start process '{displayName}'.", ex);
        }

        Task<string> readOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> readErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        Task waitForExitTask = process.WaitForExitAsync(cancellationToken);
        if (request.Timeout is TimeSpan timeout && timeout > TimeSpan.Zero)
        {
            Task completedTask = await Task.WhenAny(waitForExitTask, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
            if (completedTask != waitForExitTask)
            {
                if (completedTask.IsCanceled)
                {
                    TryKill(process);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                throw new TimeoutException($"Process '{displayName}' exceeded the timeout of {timeout}.");
            }
        }
        else
        {
            await waitForExitTask.ConfigureAwait(false);
        }

        string output;
        string error;

        try
        {
            output = await readOutputTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        try
        {
            error = await readErrorTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        output = output.TrimEnd();
        error = error.TrimEnd();

        if (request.StreamOutput)
        {
            StreamLines(output, LogLevel.Information);
            StreamLines(error, LogLevel.Error);
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            if (!string.IsNullOrEmpty(output))
            {
                _logger.LogTrace("Process '{Command}' STDOUT:{NewLine}{StdOut}", displayName, Environment.NewLine, output);
            }

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogTrace("Process '{Command}' STDERR:{NewLine}{StdErr}", displayName, Environment.NewLine, error);
            }
        }

        return new ProcessResult(process.ExitCode, output, error);
    }

    private void StreamLines(string content, LogLevel level)
    {
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        string[] lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            _logger.Log(level, "{Line}", line);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Swallow failures while attempting to terminate the process.
        }
    }
}
