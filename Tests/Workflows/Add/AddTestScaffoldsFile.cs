using System;
using System.IO;
using System.Text.Json;
using Engine;

using Tests.Framework;

namespace Tests.Workflows.Add;

public sealed class AddTestScaffoldsFile : ITestCase
{
    public string Name => "add-test creates test file and types";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        if (!Directory.Exists(Path.Combine(seedDir, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = context.Cli.Run(Commands.Init, testDir, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }

        // Run add-test to create a home page test
        ProcessRunner.ProcessResult result = context.Cli.Run(
            $"{Commands.AddTest} frontend/pages/home/home {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.AreEqual(0, result.ExitCode, $"{Commands.AddTest} failed. Error: {result.Error}");

        // Verify file exists
        string expectedTest = Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home, Folders.Tests, "home.test.ts");
        Assert.IsTrue(File.Exists(expectedTest), $"Test file not created at {expectedTest}");

        // Verify testing package manifest + archive exist for offline installs
        string manifestPath = Path.Combine(seedDir, Folders.Webstir, "testing-package.json");
        Assert.IsTrue(File.Exists(manifestPath), $"Test package manifest missing at {manifestPath}");

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        string archiveName = manifest.RootElement.GetProperty("fileName").GetString()
            ?? throw new InvalidOperationException("Manifest missing fileName.");
        string dependencyValue = manifest.RootElement.GetProperty("dependency").GetString()
            ?? throw new InvalidOperationException("Manifest missing dependency string.");

        string toolsArchive = Path.Combine(seedDir, Folders.Webstir, archiveName);
        Assert.IsTrue(File.Exists(toolsArchive), $"Testing package archive not found at {toolsArchive}");

        // Verify package.json references the local archive dependency
        string packageJsonPath = Path.Combine(seedDir, Files.PackageJson);
        Assert.IsTrue(File.Exists(packageJsonPath), $"{Files.PackageJson} not found");

        string packageJson = File.ReadAllText(packageJsonPath);
        Assert.Contains($"\"@electric-coding-llc/webstir-test\": \"{dependencyValue}\"", packageJson);
    }
}
