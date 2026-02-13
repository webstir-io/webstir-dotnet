using System;
using System.Globalization;
using System.IO;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;
using Xunit.Sdk;

namespace Tester.Workflows.Watch;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class WatchWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public WatchWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    private const string DevServiceReadySignal = "Dev Service is running.";

    private static int ReservePort()
    {
        using System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void WatchStartsAndSignalsReady()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping watch workflow: framework packages not available (set NPM_TOKEN).");
        }

        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        string? previousWebPort = Environment.GetEnvironmentVariable("AppSettings__WebServerPort");
        string? previousApiPort = Environment.GetEnvironmentVariable("AppSettings__ApiServerPort");
        try
        {
            Environment.SetEnvironmentVariable("AppSettings__WebServerPort", ReservePort().ToString(CultureInfo.InvariantCulture));
            Environment.SetEnvironmentVariable("AppSettings__ApiServerPort", ReservePort().ToString(CultureInfo.InvariantCulture));

            TestCaseContext context = _fixture.Context;
            string testDir = context.OutPath;
            Directory.CreateDirectory(testDir);
            string projectName = "seed-watch";
            string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

            string seedBuild = Path.Combine(seedDir, Folders.Build);
            if (Directory.Exists(seedBuild))
            {
                try
                {
                    Directory.Delete(seedBuild, recursive: true);
                }
                catch
                {
                    // Ignore cleanup failure; watch produces a fresh build.
                }
            }

            string configurationPath = Path.Combine(seedDir, "webstir.providers.json");
            File.WriteAllText(configurationPath, """
{
  "frontend": "@webstir-io/webstir-frontend"
}
""");

            ProcessResult result = context.Run(
                $"{Commands.Watch} {ProjectOptions.ProjectName} {projectName}",
                testDir,
                timeoutMs: 30000,
                waitForSignal: DevServiceReadySignal);

            Assert.True(
                result.ReadySignalReceived,
                $"Watch mode did not start - readiness message not received. ExitCode={result.ExitCode}{Environment.NewLine}stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{result.StandardError}");
            context.AssertNoCompilationErrors(result);
            Assert.True(result.StandardOutput.Length + result.StandardError.Length > 0, "watch command produced no output");

            string combinedOutput = $"{result.StandardOutput}{Environment.NewLine}{result.StandardError}";
            Assert.Contains("@webstir-io/webstir-frontend", combinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("entry point(s)", combinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("API server running", combinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Backend health probe succeeded", combinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AppSettings__WebServerPort", previousWebPort);
            Environment.SetEnvironmentVariable("AppSettings__ApiServerPort", previousApiPort);
        }
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void WatchSkipsReadinessAndHealthWithToggles()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping watch workflow: framework packages not available (set NPM_TOKEN).");
        }

        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        // Set toggles so NodeServer skips waiting and health check
        string? prevReady = Environment.GetEnvironmentVariable("WEBSTIR_BACKEND_WAIT_FOR_READY");
        string? prevHealth = Environment.GetEnvironmentVariable("WEBSTIR_BACKEND_HEALTHCHECK");
        string? previousWebPort = Environment.GetEnvironmentVariable("AppSettings__WebServerPort");
        string? previousApiPort = Environment.GetEnvironmentVariable("AppSettings__ApiServerPort");
        try
        {
            Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_WAIT_FOR_READY", "skip");
            Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_HEALTHCHECK", "skip");
            Environment.SetEnvironmentVariable("AppSettings__WebServerPort", ReservePort().ToString(CultureInfo.InvariantCulture));
            Environment.SetEnvironmentVariable("AppSettings__ApiServerPort", ReservePort().ToString(CultureInfo.InvariantCulture));

            TestCaseContext context = _fixture.Context;
            string testDir = context.OutPath;
            Directory.CreateDirectory(testDir);
            string projectName = "seed-watch";
            string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

            string configurationPath = Path.Combine(seedDir, "webstir.providers.json");
            File.WriteAllText(configurationPath, """
{
  "frontend": "@webstir-io/webstir-frontend"
}
""");

            ProcessResult result = context.Run(
                $"{Commands.Watch} {ProjectOptions.ProjectName} {projectName}",
                testDir,
                timeoutMs: 30000,
                waitForSignal: DevServiceReadySignal);

            Assert.True(
                result.ReadySignalReceived,
                $"Watch mode did not start - readiness message not received. ExitCode={result.ExitCode}{Environment.NewLine}stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{result.StandardError}");
            string combinedOutput = $"{result.StandardOutput}{Environment.NewLine}{result.StandardError}";
            Assert.Contains("Skipping backend ready signal wait", combinedOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Backend health probe disabled", combinedOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_WAIT_FOR_READY", prevReady);
            Environment.SetEnvironmentVariable("WEBSTIR_BACKEND_HEALTHCHECK", prevHealth);
            Environment.SetEnvironmentVariable("AppSettings__WebServerPort", previousWebPort);
            Environment.SetEnvironmentVariable("AppSettings__ApiServerPort", previousApiPort);
        }
    }
}
