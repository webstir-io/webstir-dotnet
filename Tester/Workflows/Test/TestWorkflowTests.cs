using System;
using System.IO;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;
using Xunit.Sdk;

namespace Tester.Workflows.Test;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class TestWorkflowTests
{
    private const string ProjectName = "backend-tests";
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
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping test workflow: framework packages not available (set NPM_TOKEN).");
        }

        TestCaseContext context = _fixture.Context;
        string testRoot = context.OutPath;
        Directory.CreateDirectory(testRoot);

        string projectDirectory = Path.Combine(testRoot, ProjectName);
        if (Directory.Exists(projectDirectory))
        {
            Directory.Delete(projectDirectory, recursive: true);
        }

        ProcessResult init = context.Run(
            $"{Commands.Init} {InitModes.Api} {ProjectName}",
            testRoot,
            timeoutMs: 20000);
        Assert.Equal(0, init.ExitCode);

        string backendTestsDirectory = Path.Combine(projectDirectory, Folders.Src, Folders.Backend, Folders.Tests);
        Directory.CreateDirectory(backendTestsDirectory);

        string backendTestFile = Path.Combine(backendTestsDirectory, BackendTestFileName);
        File.WriteAllText(backendTestFile, SampleBackendTestContent);

        ProcessResult result = context.Run(
            $"{Commands.Test} {ProjectOptions.ProjectName} {ProjectName}",
            testRoot,
            timeoutMs: 30000);

        context.AssertNoCompilationErrors(result);
        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("No tests found", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("All tests passed", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        string compiledBackendTest = Path.Combine(projectDirectory, Folders.Build, Folders.Backend, Folders.Tests, BackendTestFileName.Replace(".ts", ".js", StringComparison.Ordinal));
        Assert.True(File.Exists(compiledBackendTest), "Compiled backend test output missing.");
    }

    private const string SampleBackendTestContent = "import { test, assert } from '@webstir-io/webstir-testing';\n\n" +
        "test('backend sample passes', () => {\n" +
        "  assert.isTrue(true);\n" +
        "});\n";
}
