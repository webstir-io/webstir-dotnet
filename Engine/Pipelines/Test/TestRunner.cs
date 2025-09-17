using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Engine.Extensions;

namespace Engine.Pipelines.Test;

public sealed class TestRunner
{
    public static async Task<RunResult> RunAsync(AppWorkspace workspace, IEnumerable<string> compiledFiles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(compiledFiles);

        List<string> files = [.. compiledFiles];
        DateTimeOffset start = DateTimeOffset.UtcNow;

        string runnerPath = workspace.WorkingPath.Combine(
            Folders.NodeModules,
            "@webstir",
            "test",
            "dist",
            "cli.js");

        if (!File.Exists(runnerPath))
        {
            TestResult single = new(
                TestConstants.Messages.RunnerMissingTag,
                runnerPath,
                false,
                TestConstants.Messages.RunnerMissingInstructions,
                0);

            return new RunResult(0, 1, 1, 0, [single]);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = TestConstants.NodeExe,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workspace.WorkingPath
        };

        startInfo.ArgumentList.Add(runnerPath);

        using Process proc = new()
        {
            StartInfo = startInfo
        };

        proc.Start();

        string input = JsonSerializer.Serialize(files);
        await proc.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
        await proc.StandardInput.FlushAsync(cancellationToken);
        proc.StandardInput.Close();

        string output = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await proc.StandardError.ReadToEndAsync(cancellationToken);
        await proc.WaitForExitAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            // If nothing returned, treat as a single failure with stderr message
            TestResult single = new(
                TestConstants.Messages.RunnerErrorTag,
                string.Empty,
                false,
                string.IsNullOrWhiteSpace(error) ? TestConstants.Messages.RunnerNoOutput : error,
                0);
            return new RunResult(0, 1, 1, (long)(DateTimeOffset.UtcNow - start).TotalMilliseconds, [single]);
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(output);
            JsonElement root = doc.RootElement;
            int passed = root.GetProperty(TestConstants.JsonKeys.Passed).GetInt32();
            int failed = root.GetProperty(TestConstants.JsonKeys.Failed).GetInt32();
            int total = root.GetProperty(TestConstants.JsonKeys.Total).GetInt32();
            long duration = root.GetProperty(TestConstants.JsonKeys.DurationMs).GetInt64();
            List<TestResult> results = [];
            foreach (JsonElement e in root.GetProperty(TestConstants.JsonKeys.Results).EnumerateArray())
            {
                string name = e.GetProperty(TestConstants.JsonKeys.Name).GetString() ?? string.Empty;
                string file = e.GetProperty(TestConstants.JsonKeys.File).GetString() ?? string.Empty;
                bool ok = e.GetProperty(TestConstants.JsonKeys.Passed).GetBoolean();
                string? message = e.TryGetProperty(TestConstants.JsonKeys.Message, out JsonElement m) && m.ValueKind != JsonValueKind.Null
                    ? m.GetString()
                    : null;
                long d = e.GetProperty(TestConstants.JsonKeys.DurationMs).GetInt64();
                results.Add(new TestResult(name, file, ok, message, d));
            }

            return new RunResult(passed, failed, total, duration, results);
        }
        catch
        {
            TestResult single = new(TestConstants.Messages.RunnerParseErrorTag, string.Empty, false, output, 0);
            return new RunResult(0, 1, 1, (long)(DateTimeOffset.UtcNow - start).TotalMilliseconds, [single]);
        }
    }

    public static async Task<RunResult> RunForWorkspaceAsync(AppWorkspace workspace, CancellationToken cancellationToken)
    {
        IEnumerable<string> source = TestDiscovery.FindSourceTests(workspace);
        IEnumerable<string> compiled = TestDiscovery.MapToCompiled(source, workspace);
        return await RunAsync(workspace, compiled, cancellationToken);
    }
}
