using System;
using System.IO;
using Engine;
using Tests.Framework;

namespace Tests.Workflows.Test;

public sealed class BackendTestsExecute : ITestCase
{
    private const string ProjectName = "backend-tests";
    private const string BackendTestFileName = "health.test.ts";

    public string Name => "webstir test executes backend suites";

    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testRoot = Paths.OutPath;
        Directory.CreateDirectory(testRoot);

        string projectDirectory = Path.Combine(testRoot, ProjectName);
        if (Directory.Exists(projectDirectory))
        {
            Directory.Delete(projectDirectory, recursive: true);
        }

        ProcessRunner.ProcessResult init = context.Cli.Run(
            $"{Commands.Init} {InitOptions.ServerOnly} {ProjectName}",
            testRoot,
            timeoutMs: 20000);
        Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");

        string backendTestsDirectory = Path.Combine(projectDirectory, Folders.Src, Folders.Backend, Folders.Tests);
        Directory.CreateDirectory(backendTestsDirectory);

        string backendTestFile = Path.Combine(backendTestsDirectory, BackendTestFileName);
        File.WriteAllText(backendTestFile, SampleBackendTestContent);

        ProcessRunner.ProcessResult result = context.Cli.Run(
            $"{Commands.Test} {ProjectOptions.ProjectName} {ProjectName}",
            testRoot,
            timeoutMs: 30000);

        context.AssertNoCompilationErrors(result);
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Test} failed. Error: {result.Error}");
        Assert.DoesNotContain("No tests found", result.Output);
        Assert.Contains("All tests passed", result.Output);

        string compiledBackendTest = Path.Combine(projectDirectory, Folders.Build, Folders.Backend, Folders.Tests, BackendTestFileName.Replace(".ts", ".js", StringComparison.Ordinal));
        Assert.IsTrue(File.Exists(compiledBackendTest), "Compiled backend test output missing.");
    }

    private const string SampleBackendTestContent = "import { test, assert } from '@electric-coding-llc/webstir-test';\n\n" +
        "test('backend sample passes', () => {\n" +
        "  assert.isTrue(true);\n" +
        "});\n";
}
