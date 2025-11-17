using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Utilities.Process;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        spec.Validate();

        await using ProcessHandle handle = await StartInternalAsync(spec, cancellationToken).ConfigureAwait(false);

        if (spec.ReadySignal is not null && spec.WaitForReadySignalOnStart)
        {
            bool ready = await handle.WaitForReadyAsync(spec.ReadySignalTimeout, cancellationToken).ConfigureAwait(false);
            if (!ready)
            {
                await handle.StopAsync(spec.TerminationMethod, cancellationToken).ConfigureAwait(false);
                ProcessResult timeoutResult = await handle.WaitForExitAsync(null, CancellationToken.None).ConfigureAwait(false);
                return timeoutResult;
            }
        }

        return await handle.WaitForExitAsync(spec.ExitTimeout, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IProcessHandle> StartAsync(ProcessSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        spec.Validate();

        ProcessHandle handle = await StartInternalAsync(spec, cancellationToken).ConfigureAwait(false);

        if (spec.ReadySignal is not null && spec.WaitForReadySignalOnStart)
        {
            bool ready = await handle.WaitForReadyAsync(spec.ReadySignalTimeout, cancellationToken).ConfigureAwait(false);
            if (!ready)
            {
                await handle.StopAsync(spec.TerminationMethod, cancellationToken).ConfigureAwait(false);
                throw new TimeoutException(
                    $"Process '{spec.FileName}' did not emit ready signal '{spec.ReadySignal}' within {spec.ReadySignalTimeout}.");
            }
        }

        return handle;
    }

    private static Task<ProcessHandle> StartInternalAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        System.Diagnostics.Process process = new()
        {
            StartInfo = BuildStartInfo(spec),
            EnableRaisingEvents = true
        };

        ProcessHandle handle = new(process, spec);
        handle.Attach();

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{spec.FileName}'.");
        }

        handle.NotifyStarted();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return Task.FromResult(handle);
    }

    private static System.Diagnostics.ProcessStartInfo BuildStartInfo(ProcessSpec spec)
    {
        System.Diagnostics.ProcessStartInfo startInfo = new()
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            WorkingDirectory = spec.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = spec.RedirectStandardInput,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (KeyValuePair<string, string?> kvp in spec.EnvironmentVariables)
        {
            if (kvp.Value is null)
            {
                startInfo.Environment.Remove(kvp.Key);
            }
            else
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        return startInfo;
    }
}
