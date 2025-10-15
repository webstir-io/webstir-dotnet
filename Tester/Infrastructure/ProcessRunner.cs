using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Tester.Infrastructure;

public static class ProcessRunner
{
    public sealed class ProcessResult
    {
        public int ExitCode
        {
            get; set;
        }

        public string Output { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;

        public bool TimedOut
        {
            get; set;
        }

        public bool ReceivedReadySignal
        {
            get; set;
        }
    }

    public static ProcessResult Run(ProcessRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = options.FileName,
                Arguments = options.Arguments,
                WorkingDirectory = options.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        StringBuilder output = new();
        StringBuilder error = new();
        TaskCompletionSource<bool>? readySignalReceived = options.WaitForSignal is null ? null : new();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            output.AppendLine(e.Data);
            if (options.WaitForSignal is not null && e.Data.Contains(options.WaitForSignal, StringComparison.Ordinal))
            {
                readySignalReceived?.TrySetResult(true);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            error.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        bool timedOut;
        bool receivedSignal = false;

        if (options.WaitForSignal is not null)
        {
            receivedSignal = readySignalReceived!.Task.Wait(options.WaitForSignalTimeoutMs);
            SendTerminationSignal(process, options.TerminationMethod);
            timedOut = !process.WaitForExit(options.ExitTimeoutMs);
        }
        else
        {
            timedOut = !process.WaitForExit(options.ExitTimeoutMs);
        }

        if (timedOut)
        {
            KillProcessTree(process);
            process.WaitForExit();
        }

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            Output = output.ToString(),
            Error = error.ToString(),
            TimedOut = timedOut,
            ReceivedReadySignal = receivedSignal
        };
    }

    private static void SendTerminationSignal(Process process, TerminationMethod method)
    {
        switch (method)
        {
            case TerminationMethod.CtrlC:
                if (!OperatingSystem.IsWindows())
                {
                    using Process? killProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "kill",
                        Arguments = $"-INT {process.Id}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    killProcess?.WaitForExit();
                }
                else
                {
                    KillProcessTree(process);
                }

                break;

            case TerminationMethod.Kill:
                KillProcessTree(process);
                break;
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                using Process? killProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "kill",
                    Arguments = $"-TERM -{process.Id}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                killProcess?.WaitForExit(1000);
            }
            else
            {
                using Process? killProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/F /T /PID {process.Id}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                killProcess?.WaitForExit(1000);
            }
        }
        catch
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Ignore failures when killing the process tree.
            }
        }
    }
}

public sealed class ProcessRunOptions
{
    public required string FileName
    {
        get; set;
    }

    public required string Arguments
    {
        get; set;
    }

    public required string WorkingDirectory
    {
        get; set;
    }

    /// <summary>
    ///     Maximum time (milliseconds) to wait for the process to exit after completion.
    /// </summary>
    public int ExitTimeoutMs
    {
        get; set;
    } = 10000;

    /// <summary>
    ///     Optional signal substring to wait for before terminating interactive processes.
    /// </summary>
    public string? WaitForSignal
    {
        get; set;
    }

    public int WaitForSignalTimeoutMs
    {
        get; set;
    } = 5000;

    public TerminationMethod TerminationMethod
    {
        get; set;
    } = TerminationMethod.Kill;
}

public enum TerminationMethod
{
    Kill,
    CtrlC
}
