using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Utilities.Process;
using Tester.Infrastructure;
using Xunit;

namespace Tester.FrameworkPackages;

[Collection("PackageAutomationNonParallel")]
public sealed class PackageAutomationIntegrationTests
{
    [Fact]
    public void ReleaseDryRunSinglePackage()
    {
        using PackageCliWorkspace workspace = PackageCliWorkspace.Create("single-change");
        workspace.ModifyFile(Path.Combine("Framework", "Frontend", "src", "integration-change.txt"), "// integration change");

        ProcessResult result = workspace.RunFramework("-- packages release --dry-run", timeoutMs: 25000);
        Assert.Equal(0, result.ExitCode);

        using JsonDocument summary = workspace.ReadSummary();
        AssertSummaryStatus(summary, "@webstir-io/webstir-frontend", "planned-build");
        AssertSummaryStatus(summary, "@webstir-io/webstir-testing", "unchanged");
        AssertSummaryStatus(summary, "@webstir-io/webstir-backend", "unchanged");
        AssertSummaryDryRun(summary, expected: true);
    }

    [Fact]
    public void ReleaseDryRunMultiplePackages()
    {
        using PackageCliWorkspace workspace = PackageCliWorkspace.Create("multi-change");
        workspace.ModifyFile(Path.Combine("Framework", "Frontend", "src", "multi-change.ts"), "// frontend change");
        workspace.ModifyFile(Path.Combine("Framework", "Testing", "specs", "multi-change.test.ts"), "// testing change");

        ProcessResult result = workspace.RunFramework("-- packages release --dry-run", timeoutMs: 25000);
        Assert.Equal(0, result.ExitCode);

        using JsonDocument summary = workspace.ReadSummary();
        AssertSummaryStatus(summary, "@webstir-io/webstir-frontend", "planned-build");
        AssertSummaryStatus(summary, "@webstir-io/webstir-testing", "planned-build");
        AssertSummaryDryRun(summary, expected: true);
    }

    [Fact]
    public void ReleaseDryRunNoChanges()
    {
        using PackageCliWorkspace workspace = PackageCliWorkspace.Create("no-change");

        ProcessResult result = workspace.RunFramework("-- packages release --dry-run", timeoutMs: 25000);
        Assert.Equal(0, result.ExitCode);

        string output = string.Concat(result.StandardOutput, result.StandardError);
        Assert.Contains("No framework packages matched the selection.", output);

        using JsonDocument summary = workspace.ReadSummary();
        AssertSummaryStatus(summary, "@webstir-io/webstir-frontend", "unchanged");
        AssertSummaryStatus(summary, "@webstir-io/webstir-testing", "unchanged");
        AssertSummaryStatus(summary, "@webstir-io/webstir-backend", "unchanged");
    }

    [Fact]
    public void PublishDryRunProducesSummary()
    {
        using PackageCliWorkspace workspace = PackageCliWorkspace.Create("publish-dryrun");

        ProcessResult result = workspace.RunFramework("-- packages publish --dry-run --all", timeoutMs: 30000);
        Assert.Equal(0, result.ExitCode);

        using JsonDocument summary = workspace.ReadSummary();
        AssertSummaryDryRun(summary, expected: true);
        AssertSummaryStatus(summary, "@webstir-io/webstir-frontend", "planned-publish");
        AssertSummaryStatus(summary, "@webstir-io/webstir-testing", "planned-publish");
    }

    [Fact]
    public void PublishFailsWithoutToken()
    {
        using PackageCliWorkspace workspace = PackageCliWorkspace.Create("publish-missing-token");

        string? originalToken = Environment.GetEnvironmentVariable("NPM_TOKEN");
        string? originalConfig = Environment.GetEnvironmentVariable("NPM_CONFIG_USERCONFIG");
        string? originalSkip = Environment.GetEnvironmentVariable("WEBSTIR_SKIP_NPM_AUTH");
        Environment.SetEnvironmentVariable("NPM_TOKEN", null);
        Environment.SetEnvironmentVariable("NPM_CONFIG_USERCONFIG", Path.Combine(workspace.RepositoryRoot, "nonexistent", "npmrc"));
        Environment.SetEnvironmentVariable("WEBSTIR_SKIP_NPM_AUTH", "1");

        try
        {
            ProcessResult result = workspace.RunFramework("-- packages publish --all", timeoutMs: 25000);
            Assert.Equal(1, result.ExitCode);

            string output = string.Concat(result.StandardOutput, result.StandardError);
            Assert.Contains("NPM_TOKEN", output);
            Assert.Contains("does not exist", output, StringComparison.OrdinalIgnoreCase);

            using JsonDocument summary = workspace.ReadSummary();
            AssertSummaryFailure(summary);
            AssertSummaryStatus(summary, "@webstir-io/webstir-frontend", "publish-skipped");
            AssertSummaryStatus(summary, "@webstir-io/webstir-testing", "publish-skipped");
        }
        finally
        {
            Environment.SetEnvironmentVariable("NPM_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("NPM_CONFIG_USERCONFIG", originalConfig);
            Environment.SetEnvironmentVariable("WEBSTIR_SKIP_NPM_AUTH", originalSkip);
        }
    }

    private static void AssertSummaryDryRun(JsonDocument summary, bool expected)
    {
        bool actual = summary.RootElement.GetProperty("dryRun").GetBoolean();
        Assert.Equal(expected, actual);
    }

    private static void AssertSummaryStatus(JsonDocument summary, string packageName, string expectedStatus)
    {
        foreach (JsonElement package in summary.RootElement.GetProperty("packages").EnumerateArray())
        {
            if (string.Equals(package.GetProperty("name").GetString(), packageName, StringComparison.OrdinalIgnoreCase))
            {
                string status = package.GetProperty("status").GetString() ?? string.Empty;
                Assert.Equal(expectedStatus, status);
                return;
            }
        }

        throw new Xunit.Sdk.XunitException($"Package {packageName} not found in summary.");
    }

    private static void AssertSummaryFailure(JsonDocument summary)
    {
        JsonElement failure = summary.RootElement.GetProperty("failure");
        Assert.False(string.IsNullOrWhiteSpace(failure.GetString()));
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
                Path.Combine(Path.GetTempPath(), "webstir-tests", "package-automation", $"repo-{scenarioName}-{Guid.NewGuid():N}")).FullName;

            string sourceRoot = RepositoryRootLocator.Resolve();
            CopyDirectory(Path.Combine(sourceRoot, "Framework"), Path.Combine(root, "Framework"));
            CopyDirectory(Path.Combine(sourceRoot, "Utilities"), Path.Combine(root, "Utilities"));
            InitializeGit(root);

            return new PackageCliWorkspace(root);
        }

        public ProcessResult RunFramework(string arguments, int timeoutMs)
        {
            ProcessRunner runner = new();
            ProcessSpec spec = new()
            {
                FileName = "dotnet",
                Arguments = $"run --project Framework/Framework.csproj -- {arguments.Trim()}",
                WorkingDirectory = RepositoryRoot,
                ExitTimeout = timeoutMs > 0 ? TimeSpan.FromMilliseconds(timeoutMs) : null,
                TerminationMethod = TerminationMethod.Kill,
                RedirectStandardInput = false,
                WaitForReadySignalOnStart = false
            };

            return runner.RunAsync(spec, CancellationToken.None).GetAwaiter().GetResult();
        }

        public JsonDocument ReadSummary()
        {
            string summaryPath = Path.Combine(RepositoryRoot, "artifacts", "packages-release-summary.json");
            Assert.True(File.Exists(summaryPath), $"Summary file not found at {summaryPath}.");
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
            System.Diagnostics.ProcessStartInfo startInfo = new()
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using System.Diagnostics.Process process = new()
            {
                StartInfo = startInfo
            };
            process.Start();
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            bool exited = process.WaitForExit(15000);

            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch { }
            }

            Assert.True(exited, $"git {arguments} timed out");
            Assert.Equal(0, process.ExitCode); // propagate failure context
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
