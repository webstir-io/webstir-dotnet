using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Engine;
using Microsoft.Extensions.Logging;

namespace Engine.Bridge.Module;

internal static class ModuleBuildExecutor
{
    private const string ScriptFileName = "module-host.mjs";
    private const string ResultPrefix = "WEBSTIR_MODULE_RESULT ";
    private const string EventPrefix = "WEBSTIR_MODULE_EVENT ";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ModuleBuildExecutionResult> ExecuteAsync(
        AppWorkspace workspace,
        string providerId,
        ModuleBuildMode mode,
        IReadOnlyDictionary<string, string?> environmentOverrides,
        bool incremental,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        string scriptPath = await EnsureScriptAsync(workspace, cancellationToken);

        ProcessStartInfo startInfo = new()
        {
            FileName = "node",
            WorkingDirectory = workspace.WorkingPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--provider");
        startInfo.ArgumentList.Add(providerId);
        startInfo.ArgumentList.Add("--workspace");
        startInfo.ArgumentList.Add(workspace.WorkingPath);
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add(mode switch
        {
            ModuleBuildMode.Publish => "publish",
            ModuleBuildMode.Test => "test",
            _ => "build"
        });

        if (incremental)
        {
            startInfo.ArgumentList.Add("--incremental");
            startInfo.ArgumentList.Add("true");
        }

        foreach (KeyValuePair<string, string?> entry in environmentOverrides)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                continue;
            }

            startInfo.ArgumentList.Add("--env");
            startInfo.ArgumentList.Add($"{entry.Key}={entry.Value ?? string.Empty}");
        }

        using Process process = new()
        {
            StartInfo = startInfo
        };

        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort
            }
        });

        StringBuilder stdoutBuilder = new();
        List<ModuleLogEvent> eventStream = new();
        StringBuilder stderrBuilder = new();

        process.Start();

        Task stdoutTask = ReadStreamAsync(process.StandardOutput, stdoutBuilder, eventStream, cancellationToken);
        Task stderrTask = ReadStreamAsync(process.StandardError, stderrBuilder, eventStream, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        string stderr = stderrBuilder.ToString();
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            logger.LogDebug("[module-host] {Output}", stderr.TrimEnd());
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Module provider '{providerId}' failed with exit code {process.ExitCode}. {stderr}".Trim());
        }

        string stdout = stdoutBuilder.ToString();
        string? payload = ExtractPayload(stdout);

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("Module host did not return build output.");
        }

        ModuleBuildExecutionResult? result = JsonSerializer.Deserialize<ModuleBuildExecutionResult>(payload, SerializerOptions);
        if (result is null)
        {
            throw new InvalidOperationException("Unable to deserialize module build output.");
        }

        return result with
        {
            Events = eventStream.AsReadOnly()
        };
    }

    private static async Task<string> EnsureScriptAsync(AppWorkspace workspace, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(workspace.WebstirPath);
        string targetPath = Path.Combine(workspace.WebstirPath, ScriptFileName);

        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"{Resources.ModuleHostPath}.{ScriptFileName}";

        await using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream is null)
        {
            throw new InvalidOperationException("Module host script resource not found.");
        }

        await using FileStream fileStream = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        await resourceStream.CopyToAsync(fileStream, cancellationToken);

        return targetPath;
    }

    private static string? ExtractPayload(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return null;
        }

        string[] lines = stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string line in lines)
        {
            if (line.StartsWith(ResultPrefix, StringComparison.Ordinal))
            {
                return line[ResultPrefix.Length..];
            }
        }

        return null;
    }

    private static async Task ReadStreamAsync(StreamReader reader, StringBuilder builder, List<ModuleLogEvent> events, CancellationToken cancellationToken)
    {
        char[] buffer = new char[1024];
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);

            ExtractEvents(builder, events);
        }
    }

    private static void ExtractEvents(StringBuilder builder, List<ModuleLogEvent> events)
    {
        while (true)
        {
            string content = builder.ToString();
            int newlineIndex = content.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            if (newlineIndex < 0)
            {
                return;
            }

            string line = content[..newlineIndex];
            if (!line.StartsWith(EventPrefix, StringComparison.Ordinal))
            {
                return;
            }

            builder.Remove(0, newlineIndex + Environment.NewLine.Length);

            try
            {
                string json = line[EventPrefix.Length..];
                ModuleLogEvent? evt = JsonSerializer.Deserialize<ModuleLogEvent>(json, SerializerOptions);
                if (evt is not null)
                {
                    events.Add(evt);
                }
            }
            catch
            {
                // Ignore malformed events; continue processing.
            }
        }
    }
}

internal enum ModuleBuildMode
{
    Build,
    Publish,
    Test
}

internal sealed record ModuleBuildExecutionResult(
    ModuleProviderMetadata Provider,
    ModuleBuildManifest Manifest,
    IReadOnlyList<ModuleArtifact> Artifacts,
    IReadOnlyList<ModuleLogEvent> Events);

internal sealed record ModuleProviderMetadata(
    string Id,
    string Kind,
    string Version);

internal sealed record ModuleBuildManifest(
    IReadOnlyList<string> EntryPoints,
    IReadOnlyList<string> StaticAssets,
    IReadOnlyList<ModuleDiagnostic> Diagnostics,
    [property: JsonPropertyName("module")] ModuleRuntimeManifest? Module);

internal sealed record ModuleDiagnostic(
    string Severity,
    string Message,
    string? File);

internal sealed record ModuleLogEvent(
    string Type,
    string Message);

internal sealed record ModuleArtifact(
    string Path,
    string Type);
