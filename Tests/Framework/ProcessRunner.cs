using System.Diagnostics;
using System.Text;

namespace Tests.Framework;

public class ProcessRunner
{
    public class ProcessResult
    {
        public int ExitCode
        {
            get; set;
        }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
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
            StartInfo = new()
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
        TaskCompletionSource<bool>? readySignalReceived = options.WaitForSignal == null ? null : new();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
                if (options.WaitForSignal != null && e.Data.Contains(options.WaitForSignal))
                {
                    readySignalReceived?.TrySetResult(true);
                }
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        bool timedOut = false;
        bool receivedSignal = false;

        if (options.WaitForSignal != null)
        {
            // Wait for the ready signal
            receivedSignal = readySignalReceived!.Task.Wait(options.WaitForSignalTimeoutMs);

            // Send termination signal
            SendTerminationSignal(process, options.TerminationMethod);

            // Wait for exit
            timedOut = !process.WaitForExit(options.ExitTimeoutMs);
        }
        else
        {
            // Simple timeout-based execution
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
                    // Send SIGINT (Ctrl+C) on Unix-like systems
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
                    // On Windows, sending Ctrl+C to a console process is complex
                    // For now, we'll just kill the process tree
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
                // Kill the process group on Unix
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
                // Kill process tree on Windows
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
            // Fallback to simple kill
            try
            {
                process.Kill();
            }
            catch { }
        }
    }
}

public class ProcessRunOptions
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

    // For simple command execution
    public int ExitTimeoutMs { get; set; } = 10000;

    // For interactive processes that need graceful shutdown
    public string? WaitForSignal
    {
        get; set;
    }
    public int WaitForSignalTimeoutMs { get; set; } = 5000;
    public TerminationMethod TerminationMethod { get; set; } = TerminationMethod.Kill;
}

public enum TerminationMethod
{
    Kill,
    CtrlC
}
