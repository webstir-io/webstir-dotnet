using System;
using System.IO;
using Engine;
using Tester.Infrastructure;
using Xunit;

namespace Tester.Workflows.Test;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class TestWorkflowTests
{
    private const string ProjectName = "backend-tests";
    private const string VitestProjectName = "backend-tests-vitest";
    private const string BackendTestFileName = "health.test.ts";

    private readonly SeedWorkspaceFixture _fixture;

    public TestWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void BackendTestsExecute()
    {
        TestCaseContext context = _fixture.Context;
        string testRoot = context.OutPath;
        Directory.CreateDirectory(testRoot);

        string projectDirectory = Path.Combine(testRoot, ProjectName);
        if (Directory.Exists(projectDirectory))
        {
            Directory.Delete(projectDirectory, recursive: true);
        }

        ProcessRunner.ProcessResult init = context.Run(
            $"{Commands.Init} {InitOptions.ServerOnly} {ProjectName}",
            testRoot,
            timeoutMs: 20000);
        Assert.Equal(0, init.ExitCode);

        string backendTestsDirectory = Path.Combine(projectDirectory, Folders.Src, Folders.Backend, Folders.Tests);
        Directory.CreateDirectory(backendTestsDirectory);

        string backendTestFile = Path.Combine(backendTestsDirectory, BackendTestFileName);
        File.WriteAllText(backendTestFile, SampleBackendTestContent);

        ProcessRunner.ProcessResult result = context.Run(
            $"{Commands.Test} {ProjectOptions.ProjectName} {ProjectName}",
            testRoot,
            timeoutMs: 30000);

        context.AssertNoCompilationErrors(result);
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("No tests found", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("All tests passed", result.Output, StringComparison.OrdinalIgnoreCase);

        string compiledBackendTest = Path.Combine(projectDirectory, Folders.Build, Folders.Backend, Folders.Tests, BackendTestFileName.Replace(".ts", ".js", StringComparison.Ordinal));
        Assert.True(File.Exists(compiledBackendTest), "Compiled backend test output missing.");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void VitestProviderExecutesSuite()
    {
        TestCaseContext context = _fixture.Context;
        string testRoot = context.OutPath;
        Directory.CreateDirectory(testRoot);

        string projectDirectory = Path.Combine(testRoot, VitestProjectName);
        if (Directory.Exists(projectDirectory))
        {
            Directory.Delete(projectDirectory, recursive: true);
        }

        ProcessRunner.ProcessResult init = context.Run(
            $"{Commands.Init} {InitOptions.ServerOnly} {VitestProjectName}",
            testRoot,
            timeoutMs: 20000);
        Assert.Equal(0, init.ExitCode);

        string backendTestsDirectory = Path.Combine(projectDirectory, Folders.Src, Folders.Backend, Folders.Tests);
        Directory.CreateDirectory(backendTestsDirectory);

        string backendTestFile = Path.Combine(backendTestsDirectory, BackendTestFileName);
        File.WriteAllText(backendTestFile, SampleBackendTestContent);

        string? previousProvider = Environment.GetEnvironmentVariable("WEBSTIR_TESTING_PROVIDER");
        string? previousSpec = Environment.GetEnvironmentVariable("WEBSTIR_TESTING_PROVIDER_SPEC");
        string? vitestProviderSpec = Environment.GetEnvironmentVariable("WEBSTIR_VITEST_PROVIDER_SPEC");
        try
        {
            Environment.SetEnvironmentVariable("WEBSTIR_TESTING_PROVIDER", "@webstir-io/vitest-testing");
            Environment.SetEnvironmentVariable("WEBSTIR_TESTING_PROVIDER_SPEC", vitestProviderSpec);

            ProcessRunner.ProcessResult result = context.Run(
                $"{Commands.Test} {ProjectOptions.ProjectName} {VitestProjectName}",
                testRoot,
                timeoutMs: 30000);

            if (result.ExitCode != 0 && result.Output.Contains("npm install failed", StringComparison.OrdinalIgnoreCase))
            {
                // External provider couldn't be resolved; skip validation until the package is published or a local spec is provided.
                return;
            }

            context.AssertNoCompilationErrors(result);
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("No tests found", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("All tests passed", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("✔ All tests passed", result.Output, StringComparison.Ordinal);
            Assert.Contains("[packages] Installing testing provider override", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Falling back to default runtime", result.Output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBSTIR_TESTING_PROVIDER", previousProvider);
            Environment.SetEnvironmentVariable("WEBSTIR_TESTING_PROVIDER_SPEC", previousSpec);
        }
    }

    private const string SampleBackendTestContent = "import { test, assert } from '@webstir-io/webstir-testing';\n\n" +
        "test('backend sample passes', () => {\n" +
        "  assert.isTrue(true);\n" +
        "});\n";
}
