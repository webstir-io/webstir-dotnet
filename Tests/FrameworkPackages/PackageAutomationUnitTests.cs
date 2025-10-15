using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Framework.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Tests;
using Tests.Framework;
using Framework.Services;
using FrameworkProcessRunner = Framework.Services.ProcessRunner;
using FrameworkProcessRequest = Framework.Services.ProcessRequest;
using FrameworkProcessResult = Framework.Services.ProcessResult;

namespace Tests.FrameworkPackages;

public sealed class PackageAutomationUnitTests : TestSuite
{
    public override string Name => "Framework package automation unit tests";

    public override async Task<TestResult[]> RunAsync()
    {
        List<(string TestName, Func<Task> TestAction)> tests =
        [
            ("PackageMetadataService loads manifests for enabled packages", PackageMetadataServiceLoadsEnabledManifestsAsync),
            ("PackageMetadataService explicit selection rejects disabled packages", PackageMetadataServiceExplicitSelectionRejectsDisabledAsync),
            ("PackageMetadataService detects changed packages via repository diff", PackageMetadataServiceDetectsChangedPackagesAsync),
            ("PackageMetadataService updates package and lockfile versions", PackageMetadataServiceUpdatesVersionsAsync),
            ("PackageMetadataService honors dry-run for version updates", PackageMetadataServiceHonorsDryRunAsync),
            ("RepositoryDiffService parses git output and arguments", RepositoryDiffServiceParsesOutputAsync),
            ("RepositoryDiffService surfaces git failures", RepositoryDiffServiceThrowsOnFailureAsync),
            ("ProcessRunner executes commands and captures output", ProcessRunnerRunsCommandAsync),
            ("ProcessRunner reports startup failures", ProcessRunnerThrowsWhenProcessMissingAsync),
            ("PackagePublishValidator requires authentication tokens", PackagePublishValidatorRequiresTokensAsync),
            ("PackagePublishValidator validates registries when authenticated", PackagePublishValidatorValidatesRegistriesAsync),
            ("PackagePublishValidator skips validation when no publishable packages", PackagePublishValidatorSkipsWhenNoPublishableAsync)
        ];

        return await RunTestsAsync(tests).ConfigureAwait(false);
    }

    private static async Task PackageMetadataServiceLoadsEnabledManifestsAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: true);

        StubRepositoryDiffService diff = new();
        PackageMetadataService service = new(diff, NullLogger<PackageMetadataService>.Instance);

        IReadOnlyList<PackageManifest> manifests = await service
            .GetPackagesAsync(workspace.RepositoryRoot, CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(2, manifests.Count, "Expected two enabled package manifests.");

        PackageManifest frontend = manifests.First(manifest => manifest.Key == "frontend");
        PackageManifest testing = manifests.First(manifest => manifest.Key == "testing");

        Assert.AreEqual("@webstir-io/webstir-frontend", frontend.PackageName);
        Assert.AreEqual("@webstir-io/webstir-test", testing.PackageName);
        Assert.IsTrue(frontend.IsEnabled, "Frontend package should be enabled.");
        Assert.IsTrue(testing.IsEnabled, "Testing package should be enabled.");
    }

    private static async Task PackageMetadataServiceExplicitSelectionRejectsDisabledAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: true, includeBackend: true);

        StubRepositoryDiffService diff = new();
        PackageMetadataService service = new(diff, NullLogger<PackageMetadataService>.Instance);

        IReadOnlyList<PackageManifest> manifests = await service
            .ResolveAsync(
                workspace.RepositoryRoot,
                PackageSelection.Explicit(new[] { "frontend" }),
                sinceReference: null,
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(1, manifests.Count, "Explicit selection should pick only requested package.");
        Assert.AreEqual("frontend", manifests[0].Key);

        InvalidOperationException? captured = null;
        try
        {
            service.ResolveAsync(
                workspace.RepositoryRoot,
                PackageSelection.Explicit(new[] { "backend" }),
                sinceReference: null,
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            captured = ex;
        }

        Assert.IsNotNull(captured, "Expected disabled package exception.");
        Assert.Contains("disabled", captured!.Message, "Expected disabled package message.");
    }

    private static async Task PackageMetadataServiceDetectsChangedPackagesAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: true);

        StubRepositoryDiffService diff = new()
        {
            NextResult = new RepositoryDiffResult(new[]
            {
                Path.Combine("Framework", "Frontend", "src", "index.ts"),
                Path.Combine("Framework", "Random", "ignored.txt")
            })
        };

        PackageMetadataService service = new(diff, NullLogger<PackageMetadataService>.Instance);

        IReadOnlyList<PackageManifest> manifests = await service
            .ResolveAsync(
                workspace.RepositoryRoot,
                PackageSelection.ChangedPackages,
                sinceReference: null,
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(1, manifests.Count, "Expected only the frontend package to be detected.");
        Assert.AreEqual("frontend", manifests[0].Key);
        Assert.AreEqual(workspace.RepositoryRoot, diff.LastRepositoryRoot);
        Assert.IsNotNull(diff.LastOptions, "Expected repository diff options to be captured.");
        Assert.IsTrue(diff.LastOptions!.IncludeUntracked, "Default diff should include untracked files.");

        diff.NextResult = new RepositoryDiffResult(new[]
        {
            Path.Combine("Framework", "Testing", "spec.ts")
        });

        manifests = await service
            .ResolveAsync(
                workspace.RepositoryRoot,
                PackageSelection.ChangedPackages,
                sinceReference: "HEAD~1",
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(1, manifests.Count);
        Assert.AreEqual("testing", manifests[0].Key);
        Assert.IsNotNull(diff.LastOptions);
        Assert.AreEqual("HEAD~1", diff.LastOptions!.SinceRef);
        Assert.IsFalse(diff.LastOptions.IncludeUntracked, "SinceRef should disable untracked detection.");
    }

    private static async Task PackageMetadataServiceUpdatesVersionsAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: false);

        StubRepositoryDiffService diff = new();
        PackageMetadataService service = new(diff, NullLogger<PackageMetadataService>.Instance);

        PackageManifest manifest = (await service
            .GetPackagesAsync(workspace.RepositoryRoot, CancellationToken.None)
            .ConfigureAwait(false)).Single();

        SemanticVersion version = SemanticVersion.Parse("2.0.1");

        await service.UpdatePackageVersionAsync(manifest, version, dryRun: false, CancellationToken.None).ConfigureAwait(false);

        JsonDocument packageJson = JsonDocument.Parse(await File.ReadAllTextAsync(manifest.PackageJsonPath).ConfigureAwait(false));
        Assert.AreEqual("2.0.1", packageJson.RootElement.GetProperty("version").GetString(), "package.json should reflect updated version.");

        Assert.IsNotNull(manifest.PackageLockPath, "Expected package-lock.json to exist.");
        JsonDocument packageLock = JsonDocument.Parse(await File.ReadAllTextAsync(manifest.PackageLockPath!).ConfigureAwait(false));
        Assert.AreEqual("2.0.1", packageLock.RootElement.GetProperty("version").GetString());
        Assert.AreEqual(
            "2.0.1",
            packageLock.RootElement.GetProperty("packages").GetProperty(string.Empty).GetProperty("version").GetString(),
            "Root package entry should match the updated version.");
    }

    private static async Task PackageMetadataServiceHonorsDryRunAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: false);

        StubRepositoryDiffService diff = new();
        PackageMetadataService service = new(diff, NullLogger<PackageMetadataService>.Instance);

        PackageManifest manifest = (await service
            .GetPackagesAsync(workspace.RepositoryRoot, CancellationToken.None)
            .ConfigureAwait(false)).Single();

        string packageJsonBefore = await File.ReadAllTextAsync(manifest.PackageJsonPath).ConfigureAwait(false);
        string? packageLockBefore = manifest.PackageLockPath is null
            ? null
            : await File.ReadAllTextAsync(manifest.PackageLockPath).ConfigureAwait(false);

        await service.UpdatePackageVersionAsync(manifest, SemanticVersion.Parse("3.4.5"), dryRun: true, CancellationToken.None).ConfigureAwait(false);

        string packageJsonAfter = await File.ReadAllTextAsync(manifest.PackageJsonPath).ConfigureAwait(false);
        Assert.AreEqual(packageJsonBefore, packageJsonAfter, "Dry-run should not modify package.json.");

        if (manifest.PackageLockPath is not null)
        {
            string packageLockAfter = await File.ReadAllTextAsync(manifest.PackageLockPath).ConfigureAwait(false);
            Assert.AreEqual(packageLockBefore, packageLockAfter, "Dry-run should not modify package-lock.json.");
        }
    }

    private static async Task RepositoryDiffServiceParsesOutputAsync()
    {
        string repositoryRoot = RepositoryRootLocator.Resolve();

        FakeProcessRunner runner = new()
        {
            OnRun = request =>
            {
                Assert.AreEqual("git", request.FileName, "Expected git to be executed.");
                Assert.Contains("status --porcelain=1 --untracked-files=all", request.Arguments, "Expected default status command.");
                return new ProcessResult(
                    0,
                    " M Framework/Frontend/package.json\nA  Framework/Testing/new.ts\nR  Framework/Frontend/old.ts -> Framework/Frontend/new.ts\n?? Framework/Testing/untracked.js\n",
                    string.Empty);
            }
        };

        RepositoryDiffService service = new(runner, NullLogger<RepositoryDiffService>.Instance);
        RepositoryDiffResult result = await service
            .GetStatusAsync(repositoryRoot, new RepositoryDiffOptions(), CancellationToken.None)
            .ConfigureAwait(false);

        Assert.IsTrue(result.HasChanges, "Expected parsed changes.");
        Assert.AreEqual(4, result.Paths.Count);
        Assert.IsTrue(result.Paths.Contains("Framework/Frontend/package.json"), "Missing staged file path.");
        Assert.IsTrue(result.Paths.Contains("Framework/Testing/new.ts"), "Missing added file path.");
        Assert.IsTrue(result.Paths.Contains("Framework/Frontend/new.ts"), "Missing rename target path.");
        Assert.IsTrue(result.Paths.Contains("Framework/Testing/untracked.js"), "Missing untracked file path.");
    }

    private static Task RepositoryDiffServiceThrowsOnFailureAsync()
    {
        string repositoryRoot = RepositoryRootLocator.Resolve();

        FakeProcessRunner runner = new()
        {
            OnRun = _ => new ProcessResult(1, string.Empty, "fatal: not a git repository")
        };

        RepositoryDiffService service = new(runner, NullLogger<RepositoryDiffService>.Instance);

        InvalidOperationException? captured = null;
        try
        {
            service.GetStatusAsync(repositoryRoot, new RepositoryDiffOptions(), CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            captured = ex;
        }

        Assert.IsNotNull(captured, "Expected git failure exception.");
        Assert.Contains("git status", captured!.Message, "Expected git command failure message.");
        return Task.CompletedTask;
    }

    private static async Task ProcessRunnerRunsCommandAsync()
    {
        FrameworkProcessRunner runner = new(NullLogger<FrameworkProcessRunner>.Instance);
        FrameworkProcessRequest request = new(
            "dotnet",
            "--version",
            RepositoryRootLocator.Resolve());

        FrameworkProcessResult result = await runner.RunAsync(request, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(0, result.ExitCode, "dotnet --version should succeed.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.StandardOutput), "Expected version output.");
        Assert.AreEqual(string.Empty, result.StandardError, "Expected no stderr output.");
    }

    private static Task ProcessRunnerThrowsWhenProcessMissingAsync()
    {
        FrameworkProcessRunner runner = new(NullLogger<FrameworkProcessRunner>.Instance);
        FrameworkProcessRequest request = new(
            "webstir-nonexistent-command",
            string.Empty,
            RepositoryRootLocator.Resolve());

        InvalidOperationException? captured = null;
        try
        {
            runner.RunAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            captured = ex;
        }

        Assert.IsNotNull(captured, "Expected process start failure.");
        Assert.Contains("Unable to start process", captured!.Message, "Expected startup failure wording.");
        return Task.CompletedTask;
    }

    private static Task PackagePublishValidatorRequiresTokensAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: false);

        FakePackageMetadataService metadata = new([workspace.FrontendManifest!]);
        FakeProcessRunner runner = new();
        PackagePublishValidator validator = new(metadata, runner, NullLogger<PackagePublishValidator>.Instance);

        string? originalToken = Environment.GetEnvironmentVariable("GH_PACKAGES_TOKEN");
        Environment.SetEnvironmentVariable("GH_PACKAGES_TOKEN", null);

        try
        {
            InvalidOperationException? captured = null;
            try
            {
                validator.ValidateAsync(
                    workspace.RepositoryRoot,
                    PackageSelection.AllPackages,
                    sinceReference: null,
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (InvalidOperationException ex)
            {
                captured = ex;
            }

            Assert.IsNotNull(captured, "Expected missing token failure.");
            Assert.Contains("GH_PACKAGES_TOKEN", captured!.Message, "Expected missing token message.");
            Assert.AreEqual(0, runner.Requests.Count, "Registry validation should not run when tokens are missing.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GH_PACKAGES_TOKEN", originalToken);
        }

        return Task.CompletedTask;
    }

    private static async Task PackagePublishValidatorValidatesRegistriesAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: false);

        FakePackageMetadataService metadata = new([workspace.FrontendManifest!]);
        FakeProcessRunner runner = new()
        {
            OnRun = request =>
            {
                Assert.AreEqual("npm", request.FileName);
                Assert.Contains("ping", request.Arguments, "Expected npm ping invocation.");
                Assert.Contains("https://npm.pkg.github.com", request.Arguments, "Expected registry URL.");
                return new ProcessResult(0, "pong", string.Empty);
            }
        };

        PackagePublishValidator validator = new(metadata, runner, NullLogger<PackagePublishValidator>.Instance);

        string? originalToken = Environment.GetEnvironmentVariable("GH_PACKAGES_TOKEN");
        Environment.SetEnvironmentVariable("GH_PACKAGES_TOKEN", "fake-token");

        try
        {
            await validator
                .ValidateAsync(
                    workspace.RepositoryRoot,
                    PackageSelection.AllPackages,
                    sinceReference: null,
                    CancellationToken.None)
                .ConfigureAwait(false);

            Assert.AreEqual(1, runner.Requests.Count, "Expected a single registry validation call.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GH_PACKAGES_TOKEN", originalToken);
        }
    }

    private static async Task PackagePublishValidatorSkipsWhenNoPublishableAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: false, testingEnabled: false, includeBackend: true);

        FakePackageMetadataService metadata = new([workspace.BackendManifest!]);
        FakeProcessRunner runner = new();
        PackagePublishValidator validator = new(metadata, runner, NullLogger<PackagePublishValidator>.Instance);

        await validator
            .ValidateAsync(
                workspace.RepositoryRoot,
                PackageSelection.AllPackages,
                sinceReference: null,
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual(0, runner.Requests.Count, "No registry checks should run when packages do not support publishing.");
    }

    private sealed class StubRepositoryDiffService : IRepositoryDiffService
    {
        public RepositoryDiffResult NextResult { get; set; } = new(Array.Empty<string>());

        public string? LastRepositoryRoot
        {
            get; private set;
        }

        public RepositoryDiffOptions? LastOptions
        {
            get; private set;
        }

        public Task<RepositoryDiffResult> GetStatusAsync(string repositoryRoot, RepositoryDiffOptions options, CancellationToken cancellationToken)
        {
            LastRepositoryRoot = repositoryRoot;
            LastOptions = options;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = new();

        public Func<ProcessRequest, ProcessResult>? OnRun
        {
            get; set;
        }

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            ProcessResult result = OnRun is null
                ? new ProcessResult(0, string.Empty, string.Empty)
                : OnRun(request);

            return Task.FromResult(result);
        }
    }

    private sealed class FakePackageMetadataService(IReadOnlyList<PackageManifest> manifests) : IPackageMetadataService
    {
        private readonly IReadOnlyList<PackageManifest> _manifests = manifests;

        public Task<IReadOnlyList<PackageManifest>> GetPackagesAsync(string repositoryRoot, CancellationToken cancellationToken) =>
            Task.FromResult(_manifests);

        public Task<IReadOnlyList<PackageManifest>> ResolveAsync(
            string repositoryRoot,
            PackageSelection selection,
            string? sinceReference,
            CancellationToken cancellationToken) => Task.FromResult(_manifests);

        public Task UpdatePackageVersionAsync(PackageManifest manifest, SemanticVersion version, bool dryRun, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root, PackageManifest? frontend, PackageManifest? testing, PackageManifest? backend)
        {
            RepositoryRoot = root;
            FrontendManifest = frontend;
            TestingManifest = testing;
            BackendManifest = backend;
        }

        public string RepositoryRoot
        {
            get;
        }

        public PackageManifest? FrontendManifest
        {
            get;
        }

        public PackageManifest? TestingManifest
        {
            get;
        }

        public PackageManifest? BackendManifest
        {
            get;
        }

        public static TestWorkspace WithPackages(bool frontendEnabled, bool testingEnabled, bool includeBackend = false)
        {
            string root = Directory.CreateDirectory(Path.Combine(Paths.OutPath, "package-automation", Guid.NewGuid().ToString("N"))).FullName;
            string frameworkRoot = Path.Combine(root, "Framework");
            Directory.CreateDirectory(frameworkRoot);

            PackageManifest? frontend = frontendEnabled
                ? CreateManifest(frameworkRoot, "Frontend", "@webstir-io/webstir-frontend", version: "1.0.0", enabled: true)
                : null;
            PackageManifest? testing = testingEnabled
                ? CreateManifest(frameworkRoot, "Testing", "@webstir-io/webstir-test", version: "1.0.0", enabled: true)
                : null;
            PackageManifest? backend = includeBackend
                ? CreateManifest(frameworkRoot, "Backend", "@webstir-io/webstir-backend", version: "1.0.0", enabled: false)
                : null;

            return new TestWorkspace(root, frontend, testing, backend);
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
                // Ignore cleanup failures in test sandbox.
            }
        }

        private static PackageManifest CreateManifest(
            string frameworkRoot,
            string directoryName,
            string packageName,
            string version,
            bool enabled)
        {
            string packageDirectory = Path.Combine(frameworkRoot, directoryName);
            Directory.CreateDirectory(packageDirectory);

            string packageJsonPath = Path.Combine(packageDirectory, "package.json");
            string packageLockPath = Path.Combine(packageDirectory, "package-lock.json");

            string key = directoryName.ToLowerInvariant();

            JsonSerializerOptions serializerOptions = new()
            {
                WriteIndented = true
            };

            using (FileStream stream = File.Create(packageJsonPath))
            {
                JsonSerializer.Serialize(stream, new Dictionary<string, object?>
                {
                    ["name"] = packageName,
                    ["version"] = version
                }, serializerOptions);
            }

            using (FileStream stream = File.Create(packageLockPath))
            {
                JsonSerializer.Serialize(stream, new Dictionary<string, object?>
                {
                    ["name"] = packageName,
                    ["version"] = version,
                    ["packages"] = new Dictionary<string, object?>
                    {
                        [string.Empty] = new Dictionary<string, object?>
                        {
                            ["name"] = packageName,
                            ["version"] = version
                        }
                    }
                }, serializerOptions);
            }

            HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase)
            {
                key,
                packageName
            };

            return new PackageManifest(
                key,
                packageName,
                packageDirectory,
                packageJsonPath,
                packageLockPath,
                SemanticVersion.Parse(version),
                identifiers,
                enabled);
        }
    }

}
