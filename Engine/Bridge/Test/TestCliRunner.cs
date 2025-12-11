using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Engine.Bridge.Module;

namespace Engine.Bridge.Test;

internal sealed record TestCliRunSettings(string? RuntimeFilter);

internal sealed class TestCliRunner
{
    private const string EventPrefix = "WEBSTIR_TEST ";
    private const string ModuleEventPrefix = "WEBSTIR_MODULE_EVENT ";
    private const string DefaultProviderId = "@webstir-io/webstir-testing";
    private static readonly JsonSerializerOptions ModuleEventSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppWorkspace _workspace;

    internal TestCliRunner(AppWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    internal async Task<TestCliRunResult> RunTestsAsync(
        CancellationToken cancellationToken,
        TestCliRunSettings? settings = null)
    {
        NodeRuntime.EnsureMinimumVersion();

        string providerId = ResolveProviderId();
        string scriptPath = await TestHostScript.EnsureAsync(_workspace, cancellationToken).ConfigureAwait(false);

        ProcessStartInfo startInfo = new()
        {
            FileName = "node",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _workspace.WorkingPath
        };

        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--provider");
        startInfo.ArgumentList.Add(providerId);
        startInfo.ArgumentList.Add("--workspace");
        startInfo.ArgumentList.Add(_workspace.WorkingPath);
        startInfo.Environment["WEBSTIR_WORKSPACE_ROOT"] = _workspace.WorkingPath;
        startInfo.Environment["WEBSTIR_BACKEND_BUILD_ROOT"] = _workspace.BackendBuildPath;
        startInfo.Environment["WEBSTIR_BACKEND_TEST_MANIFEST"] = _workspace.BackendManifestPath;
        startInfo.Environment["WEBSTIR_BACKEND_TEST_ENTRY"] = Path.Combine(_workspace.BackendBuildPath, "index.js");
        if (!string.IsNullOrWhiteSpace(settings?.RuntimeFilter))
        {
            startInfo.Environment["WEBSTIR_TEST_RUNTIME"] = settings.RuntimeFilter;
        }

        using Process process = new()
        {
            StartInfo = startInfo
        };

        List<TestCliTestResult> results = [];
        bool testsDiscovered = false;
        bool hadErrors = false;
        int passed = 0;
        int failed = 0;
        int total = 0;
        long durationMs = 0;

        process.OutputDataReceived += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data))
            {
                return;
            }

            HandleProcessLine(
                args.Data,
                isError: false,
                results,
                ref testsDiscovered,
                ref passed,
                ref failed,
                ref total,
                ref durationMs,
                ref hadErrors);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data))
            {
                return;
            }

            HandleProcessLine(
                args.Data,
                isError: true,
                results,
                ref testsDiscovered,
                ref passed,
                ref failed,
                ref total,
                ref durationMs,
                ref hadErrors);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        TestCliRunResult runResult = new(passed, failed, total, durationMs, results, testsDiscovered, hadErrors, process.ExitCode);

        if (runResult.Failed > 0 || runResult.HadErrors)
        {
            foreach (TestCliTestResult result in results)
            {
                if (!result.Passed)
                {
                    string detail = string.IsNullOrWhiteSpace(result.Message) ? "(no message from runner)" : result.Message!;
                    Console.Error.WriteLine($"[test] FAILED: {result.Name} — {detail}");
                }
            }
        }

        return runResult;
    }

    private string ResolveProviderId()
    {
        string? overrideId = Environment.GetEnvironmentVariable("WEBSTIR_TESTING_PROVIDER");
        if (!string.IsNullOrWhiteSpace(overrideId))
        {
            return overrideId;
        }

        string? configured = ProviderConfigurationLoader.TryGetTestingProvider(_workspace);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return DefaultProviderId;
    }

    private void HandleProcessLine(
        string line,
        bool isError,
        List<TestCliTestResult> results,
        ref bool testsDiscovered,
        ref int passed,
        ref int failed,
        ref int total,
        ref long durationMs,
        ref bool hadErrors)
    {
        if (TryHandleModuleEvent(line, ref hadErrors))
        {
            return;
        }

        if (line.StartsWith(EventPrefix, StringComparison.Ordinal))
        {
            string json = line[EventPrefix.Length..];
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                if (!root.TryGetProperty("type", out JsonElement typeElement))
                {
                    return;
                }

                string? type = typeElement.GetString();
                if (string.IsNullOrWhiteSpace(type))
                {
                    return;
                }

                switch (type)
                {
                    case "start":
                        testsDiscovered = ExtractManifestModuleCount(root) > 0;
                        break;
                    case "result":
                        HandleResultEvent(root, results, ref passed, ref failed, ref total);
                        break;
                    case "summary":
                        HandleSummaryEvent(root, ref passed, ref failed, ref total, ref durationMs);
                        break;
                    case "log":
                        HandleLogEvent(root);
                        break;
                    case "error":
                        hadErrors = true;
                        HandleErrorEvent(root);
                        break;
                }
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"[test] Unable to parse runner event: {ex.Message}\n{json}");
                hadErrors = true;
            }
        }
        else if (isError)
        {
            hadErrors = true;
            Console.Error.WriteLine(line);
        }
        else
        {
            Console.WriteLine(line);
        }
    }

    private bool TryHandleModuleEvent(string line, ref bool hadErrors)
    {
        if (!line.StartsWith(ModuleEventPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string json = line[ModuleEventPrefix.Length..];
        try
        {
            ModuleLogEvent? moduleEvent = JsonSerializer.Deserialize<ModuleLogEvent>(json, ModuleEventSerializerOptions);
            if (moduleEvent is null)
            {
                return true;
            }

            if (string.Equals(moduleEvent.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                hadErrors = true;
                Console.Error.WriteLine($"[test-provider] {moduleEvent.Message}");
            }
            else if (string.Equals(moduleEvent.Type, "warn", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"[test-provider] {moduleEvent.Message}");
            }
            else
            {
                Console.WriteLine($"[test-provider] {moduleEvent.Message}");
            }
        }
        catch (JsonException ex)
        {
            hadErrors = true;
            Console.Error.WriteLine($"[test] Unable to parse provider event: {ex.Message}");
        }

        return true;
    }

    private int ExtractManifestModuleCount(JsonElement root)
    {
        if (!root.TryGetProperty("manifest", out JsonElement manifest))
        {
            return 0;
        }

        if (!manifest.TryGetProperty("modules", out JsonElement modules) || modules.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return modules.GetArrayLength();
    }

    private void HandleResultEvent(JsonElement root, List<TestCliTestResult> results, ref int passed, ref int failed, ref int total)
    {
        if (!root.TryGetProperty("result", out JsonElement result) || result.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        string name = result.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
        string file = result.TryGetProperty("file", out JsonElement fileElement) ? fileElement.GetString() ?? string.Empty : string.Empty;
        bool ok = result.TryGetProperty("passed", out JsonElement passedElement) && passedElement.GetBoolean();
        string? message = null;
        if (result.TryGetProperty("message", out JsonElement messageElement) && messageElement.ValueKind != JsonValueKind.Null)
        {
            message = messageElement.GetString();
        }

        long durationMs = result.TryGetProperty("durationMs", out JsonElement durationElement) ? durationElement.GetInt64() : 0;

        string relativeFile = MakeRelative(file);
        results.Add(new TestCliTestResult(name, relativeFile, ok, message, durationMs));

        total += 1;
        if (ok)
        {
            passed += 1;
        }
        else
        {
            failed += 1;
        }
    }

    private void HandleSummaryEvent(JsonElement root, ref int passed, ref int failed, ref int total, ref long durationMs)
    {
        if (!root.TryGetProperty("runtime", out JsonElement runtimeElement))
        {
            return;
        }

        string? runtime = runtimeElement.GetString();
        if (!string.Equals(runtime, "all", StringComparison.Ordinal))
        {
            return;
        }

        if (!root.TryGetProperty("summary", out JsonElement summary) || summary.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (summary.TryGetProperty("passed", out JsonElement passedElement))
        {
            passed = passedElement.GetInt32();
        }

        if (summary.TryGetProperty("failed", out JsonElement failedElement))
        {
            failed = failedElement.GetInt32();
        }

        if (summary.TryGetProperty("total", out JsonElement totalElement))
        {
            total = totalElement.GetInt32();
        }

        if (summary.TryGetProperty("durationMs", out JsonElement durationElement))
        {
            durationMs = durationElement.GetInt64();
        }
    }

    private void HandleLogEvent(JsonElement root)
    {
        string level = root.TryGetProperty("level", out JsonElement levelElement) ?
            levelElement.GetString() ?? "info" :
            "info";

        string message = root.TryGetProperty("message", out JsonElement messageElement) ?
            messageElement.GetString() ?? string.Empty :
            string.Empty;

        if (string.Equals(level, "warn", StringComparison.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        else if (string.Equals(level, "error", StringComparison.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    private void HandleErrorEvent(JsonElement root)
    {
        string message = root.TryGetProperty("message", out JsonElement messageElement) ?
            messageElement.GetString() ?? string.Empty :
            string.Empty;

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();

        if (root.TryGetProperty("stack", out JsonElement stackElement))
        {
            string? stack = stackElement.GetString();
            if (!string.IsNullOrWhiteSpace(stack))
            {
                Console.WriteLine(stack);
            }
        }
    }

    private string MakeRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            string relative = Path.GetRelativePath(_workspace.WorkingPath, path);
            return string.IsNullOrWhiteSpace(relative) ? path : relative;
        }
        catch (Exception)
        {
            return path;
        }
    }
}
