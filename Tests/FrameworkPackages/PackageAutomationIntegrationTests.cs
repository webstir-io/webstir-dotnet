using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Tests;
using Tests.Framework;

namespace Tests.FrameworkPackages;

public sealed class PackageAutomationIntegrationTests : TestSuite
{
    public override string Name => "Framework package automation integration tests";

    public override async Task<TestResult[]> RunAsync()
    {
        List<(string TestName, Func<Task> TestAction)> tests =
        [
            ("packages release --dry-run captures single change summary", ReleaseDryRunSinglePackageAsync),
            ("packages release --dry-run captures multiple packages", ReleaseDryRunMultiplePackagesAsync),
            ("packages release --dry-run handles no-op", ReleaseDryRunNoChangesAsync),
            ("packages publish --dry-run records planned packages", PublishDryRunProducesSummaryAsync),
            ("packages publish fails without credentials", PublishFailsWithoutTokenAsync)
        ];

        return await RunTestsAsync(tests).ConfigureAwait(false);
    }

    private static Task ReleaseDryRunSinglePackageAsync()
    {
        using PackageCliWorkspace workspace = PackageCliWorkspace.Create("single-change");
        workspace.ModifyFile(Path.Combine("Framework", "Frontend", "src", "integration-change.txt"), "// integration change");

        ProcessRunner.ProcessResult result = workspace.RunFramework("-- packages release --dry-run", timeoutMs: 25000);
        Assert.AreEqual(0, result.ExitCode, "packages release --dry-run should succeed.");

        using JsonDocument summary = workspace.ReadSummary();
        AssertSummaryStatus(summary, "@webstir-io/webstir-frontend", "planned-build");
        AssertSummaryStatus(summary, "@webstir-io/webstir-test", "unchanged");
        AssertSummaryStatus(summary, "@webstir-io/webstir-backend", "disabled");
        AssertSummaryDryRun(summary, expected: true);
        return Task.CompletedTask;
    }

    private static Task ReleaseDryRunMultiplePackagesAsync()
    {
        using PackageCliWorkspace workspace = PackageCliWorkspace.Create("multi-change");
        workspace.ModifyFile(Path.Combine("Framework", "Frontend", "src", "multi-change.ts"), "// frontend change");
        workspace.ModifyFile(Path.Combine("Framework", "Testing", "specs", "multi-change.test.ts"), "// testing change");

        ProcessRunner.ProcessResult result = workspace.RunFramework("-- packages release --dry-run", timeoutMs: 25000);
        Assert.AreEqual(0, result.ExitCode, "packages release --dry-run should succeed.");

        using JsonDocument summary = workspace.ReadSummary();
        AssertSummaryStatus(summary, "@webstir-io/webstir-frontend", "planned-build");
        AssertSummaryStatus(summary, "@webstir-io/webstir-test", "planned-build");
        AssertSummaryDryRun(summary, expected: true);
        return Task.CompletedTask;
    }

    private static Task ReleaseDryRunNoChangesAsync()
    {
        using PackageCliWorkspace workspace = PackageCliWorkspace.Create("no-change");

        ProcessRunner.ProcessResult result = workspace.RunFramework("-- packages release --dry-run", timeoutMs: 25000);
        Assert.AreEqual(0, result.ExitCode, "packages release --dry-run should succeed even with no changes.");

        string output = string.Concat(result.Output, result.Error);
        Assert.Contains("No framework packages matched the selection.", output, "Expected no-op messaging when nothing changed.");

        using JsonDocument summary = workspace.ReadSummary();
        AssertSummaryStatus(summary, "@webstir-io/webstir-frontend", "unchanged");
        AssertSummaryStatus(summary, "@webstir-io/webstir-test", "unchanged");
        AssertSummaryStatus(summary, "@webstir-io/webstir-backend", "disabled");
        return Task.CompletedTask;
    }

    private static Task PublishDryRunProducesSummaryAsync()
    {
        using PackageCliWorkspace workspace = PackageCliWorkspace.Create("publish-dryrun");

        ProcessRunner.ProcessResult result = workspace.RunFramework("-- packages publish --dry-run --all", timeoutMs: 30000);
        Assert.AreEqual(0, result.ExitCode, "packages publish --dry-run should succeed.");

        using JsonDocument summary = workspace.ReadSummary();
        AssertSummaryDryRun(summary, expected: true);
        AssertSummaryStatus(summary, "@webstir-io/webstir-frontend", "planned-publish");
        AssertSummaryStatus(summary, "@webstir-io/webstir-test", "planned-publish");
        return Task.CompletedTask;
    }

    private static Task PublishFailsWithoutTokenAsync()
    {
        using PackageCliWorkspace workspace = PackageCliWorkspace.Create("publish-missing-token");

        string? originalToken = Environment.GetEnvironmentVariable("GH_PACKAGES_TOKEN");
        string? originalConfig = Environment.GetEnvironmentVariable("NPM_CONFIG_USERCONFIG");
        Environment.SetEnvironmentVariable("GH_PACKAGES_TOKEN", null);
        Environment.SetEnvironmentVariable("NPM_CONFIG_USERCONFIG", Path.Combine(workspace.RepositoryRoot, "nonexistent", "npmrc"));

        try
        {
            ProcessRunner.ProcessResult result = workspace.RunFramework("-- packages publish --all", timeoutMs: 25000);
            Assert.AreEqual(1, result.ExitCode, "publish should fail without credentials.");

            string output = string.Concat(result.Output, result.Error);
            Assert.Contains("GH_PACKAGES_TOKEN", output, "Expected missing token guidance.");
            Assert.Contains("does not exist", output, "Expected npm config warning.");

            using JsonDocument summary = workspace.ReadSummary();
            AssertSummaryFailure(summary);
            AssertSummaryStatus(summary, "@webstir-io/webstir-frontend", "publish-skipped");
            AssertSummaryStatus(summary, "@webstir-io/webstir-test", "publish-skipped");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GH_PACKAGES_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("NPM_CONFIG_USERCONFIG", originalConfig);
        }

        return Task.CompletedTask;
    }

    private static void AssertSummaryDryRun(JsonDocument summary, bool expected)
    {
        bool actual = summary.RootElement.GetProperty("dryRun").GetBoolean();
        Assert.AreEqual(expected, actual, "Unexpected dry run flag in summary.");
    }

    private static void AssertSummaryStatus(JsonDocument summary, string packageName, string expectedStatus)
    {
        foreach (JsonElement package in summary.RootElement.GetProperty("packages").EnumerateArray())
        {
            if (string.Equals(package.GetProperty("name").GetString(), packageName, StringComparison.OrdinalIgnoreCase))
            {
                string status = package.GetProperty("status").GetString() ?? string.Empty;
                Assert.AreEqual(expectedStatus, status, $"Unexpected status for {packageName}.");
                return;
            }
        }

        Assert.Fail($"Package {packageName} not found in summary.");
    }

    private static void AssertSummaryFailure(JsonDocument summary)
    {
        JsonElement failure = summary.RootElement.GetProperty("failure");
        Assert.IsFalse(string.IsNullOrWhiteSpace(failure.GetString()), "Expected failure message in summary.");
    }

    private sealed class PackageCliWorkspace : IDisposable
    {
        private PackageCliWorkspace(string repositoryRoot)
        {
            RepositoryRoot = repositoryRoot;
        }

        public string RepositoryRoot
        {
            get;
        }

        public static PackageCliWorkspace Create(string scenarioName)
        {
            string root = Directory.CreateDirectory(
                Path.Combine(Paths.OutPath, "package-automation", $"repo-{scenarioName}-{Guid.NewGuid():N}")).FullName;

            string sourceRoot = RepositoryRootLocator.Resolve();
            CopyDirectory(Path.Combine(sourceRoot, "Framework"), Path.Combine(root, "Framework"));
            InitializeGit(root);

            return new PackageCliWorkspace(root);
        }

        public ProcessRunner.ProcessResult RunFramework(string arguments, int timeoutMs)
        {
            FrameworkConsole console = new();
            return console.Run($"-- {arguments.Trim()}", RepositoryRoot, timeoutMs);
        }

        public JsonDocument ReadSummary()
        {
            string summaryPath = Path.Combine(RepositoryRoot, "artifacts", "packages-release-summary.json");
            Assert.IsTrue(File.Exists(summaryPath), $"Summary file not found at {summaryPath}.");
            string json = File.ReadAllText(summaryPath);
            return JsonDocument.Parse(json);
        }

        public void ModifyFile(string relativePath, string content)
        {
            string fullPath = Path.Combine(RepositoryRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.AppendAllText(fullPath, $"{content}{Environment.NewLine}");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RepositoryRoot))
                {
                    Directory.Delete(RepositoryRoot, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }

        private static void InitializeGit(string root)
        {
            RunGit(root, "init");
            RunGit(root, "config user.email test@example.com");
            RunGit(root, "config user.name Webstir Tests");
            RunGit(root, "add .");
            RunGit(root, "commit -m \"initial\" --allow-empty");
        }

        private static void RunGit(string workingDirectory, string arguments)
        {
            ProcessRunner.ProcessResult result = ProcessRunner.Run(new ProcessRunOptions
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                ExitTimeoutMs = 15000
            });

            Assert.AreEqual(0, result.ExitCode, $"git {arguments} failed: {result.Error}");
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkip(directory))
                {
                    continue;
                }

                string relative = Path.GetRelativePath(source, directory);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkip(file))
                {
                    continue;
                }

                string relative = Path.GetRelativePath(source, file);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }

        private static bool ShouldSkip(string path)
        {
            string normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            foreach (string segment in normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                    segment.Equals(".git", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
