using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Engine.Bridge.Module;
using Engine.Bridge.Test;
using Engine.Helpers;
using Engine.Extensions;
using Engine.Interfaces;
using Engine.Models;
using Framework.Packaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Engine.Workflows;

public sealed class TestWorkflow(
    AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers,
    IOptions<AppSettings> options,
    ILogger<TestWorkflow> logger) : BaseWorkflow(context, workers)
{
    private readonly AppSettings _settings = options.Value;
    private readonly ILogger<TestWorkflow> _logger = logger;
    public override string WorkflowName => Commands.Test;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string? runtimeFilter = RuntimeOptionParser.Parse(args);
        WorkspaceProfile workspaceProfile = WorkspaceProfile;
        WorkspaceProfile effectiveProfile = ApplyRuntimeFilter(workspaceProfile, runtimeFilter);
        LogRuntimeScope(workspaceProfile, runtimeFilter, effectiveProfile);

        await ExecuteBuildWithFilterAsync(effectiveProfile);
        await CompileTypeScriptAsync();
        if (effectiveProfile.HasBackend)
        {
            await CompileBackendAsync();
        }

        PackageEnsureSummary ensureSummary = await TestPackageUtilities.EnsurePackageAsync(Context);
        TestPackageUtilities.LogEnsureMessages(ensureSummary);

        TestCliRunner runner = new(Context);
        TestCliRunResult runResult = await runner.RunTestsAsync(
            CancellationToken.None,
            new TestCliRunSettings(runtimeFilter));

        if (!runResult.TestsDiscovered)
        {
            Console.WriteLine("No tests found under src/**/tests/");
            return;
        }

        PrintResults(runResult);

        if (runResult.Failed > 0 || runResult.HadErrors || runResult.ExitCode != 0)
        {
            Environment.ExitCode = 1;
        }
    }

    private static void PrintResults(TestCliRunResult result)
    {
        bool anyFailures = false;
        foreach (TestCliTestResult testResult in result.Results)
        {
            if (testResult.Passed)
            {
                continue;
            }

            anyFailures = true;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("FAIL ");
            Console.ResetColor();
            Console.WriteLine(testResult.Name);
            if (!string.IsNullOrWhiteSpace(testResult.Message))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  {testResult.File}");
                Console.ResetColor();
                Console.WriteLine($"  {testResult.Message}");
            }
        }

        if (anyFailures)
        {
            Console.WriteLine();
            Console.WriteLine($"Passed: {result.Passed}, Failed: {result.Failed}, Total: {result.Total} in {result.DurationMs}ms");
            return;
        }

        if (result.Total > 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("✔ ");
            Console.ResetColor();
            Console.WriteLine("All tests passed");
        }
    }

    private static WorkspaceProfile ApplyRuntimeFilter(WorkspaceProfile profile, string? runtimeFilter) =>
        string.Equals(runtimeFilter, "backend", StringComparison.OrdinalIgnoreCase)
            ? profile with
            {
                HasFrontend = false,
                HasBackend = true
            }
            : string.Equals(runtimeFilter, "frontend", StringComparison.OrdinalIgnoreCase)
                ? profile with
                {
                    HasFrontend = true,
                    HasBackend = false
                }
                : profile;

    private async Task ExecuteBuildWithFilterAsync(WorkspaceProfile profile) =>
        await ExecuteBuildAsync(profile);

    private async Task CompileTypeScriptAsync() => await TypeScriptCompiler.CompileAsync(Context);

    private async Task CompileBackendAsync()
    {
        Dictionary<string, string?> env = new(StringComparer.Ordinal)
        {
            ["API_PORT"] = _settings.ApiServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["WEB_PORT"] = _settings.WebServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        string providerId = ResolveBackendProvider();
        ModuleBuildExecutionResult result = await ModuleBuildExecutor.ExecuteAsync(
            Context,
            providerId,
            ModuleBuildMode.Test,
            env,
            incremental: false,
            _logger,
            CancellationToken.None);

        LogBackendModuleResult(result);
    }

    private string ResolveBackendProvider()
    {
        string? overrideId = Environment.GetEnvironmentVariable("WEBSTIR_BACKEND_PROVIDER");
        if (!string.IsNullOrWhiteSpace(overrideId))
        {
            return overrideId;
        }

        string? configured = ProviderConfigurationLoader.TryGetBackendProvider(Context);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return "@webstir-io/webstir-backend";
    }

    private void LogBackendModuleResult(ModuleBuildExecutionResult result)
    {
        _logger.LogDebug(
            "[backend] Provider {ProviderId} produced {EntryCount} entry point(s) during tests.",
            result.Provider.Id,
            result.Manifest.EntryPoints.Count);

        if (result.Manifest.Module?.Routes is { } routes)
        {
            _logger.LogDebug(
                "[backend] Provider {ProviderId} reported {RouteCount} route(s) in manifest.",
                result.Provider.Id,
                routes.Count);
        }

        foreach (ModuleDiagnostic diagnostic in result.Manifest.Diagnostics)
        {
            if (string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[backend] {Message}", diagnostic.Message);
            }
            else if (string.Equals(diagnostic.Severity, "warn", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[backend] {Message}", diagnostic.Message);
            }
            else
            {
                _logger.LogDebug("[backend] {Message}", diagnostic.Message);
            }
        }

        foreach (ModuleLogEvent evt in result.Events)
        {
            if (string.Equals(evt.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[backend] {Message}", evt.Message);
            }
            else if (string.Equals(evt.Type, "warn", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[backend] {Message}", evt.Message);
            }
            else
            {
                _logger.LogDebug("[backend] {Message}", evt.Message);
            }
        }
    }

    private void LogRuntimeScope(WorkspaceProfile workspaceProfile, string? runtimeFilter, WorkspaceProfile effectiveProfile)
    {
        string workspaceLabel = DescribeProfile(workspaceProfile);
        string filterLabel = string.IsNullOrWhiteSpace(runtimeFilter) ? "auto" : runtimeFilter!;
        string effectiveLabel = DescribeProfile(effectiveProfile);

        _logger.LogDebug(
            "[{Workflow}] Runtime scope — workspace: {Workspace}, filter: {Filter}, running: {Effective}.",
            WorkflowName,
            workspaceLabel,
            filterLabel,
            effectiveLabel);
    }

    private static string DescribeProfile(WorkspaceProfile profile) => (profile.HasFrontend, profile.HasBackend) switch
    {
        (true, true) => "frontend+backend",
        (true, false) => "frontend-only",
        (false, true) => "backend-only",
        _ => "none"
    };
}
