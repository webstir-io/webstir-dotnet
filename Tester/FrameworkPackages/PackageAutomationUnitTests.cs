using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Framework.Services;
using Framework.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Tester.Infrastructure;
using Xunit;
using Utilities.Process;

namespace Tester.FrameworkPackages;

[Collection("PackageAutomationNonParallel")]
public sealed class PackageAutomationUnitTests
{
    [Fact]
    public async Task PackageMetadataServiceLoadsEnabledManifestsAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: true, includeBackend: true);

        StubRepositoryDiffService diff = new();
        PackageMetadataService service = new(diff, NullLogger<PackageMetadataService>.Instance);

        IReadOnlyList<PackageManifest> manifests = await service
            .GetPackagesAsync(workspace.RepositoryRoot, CancellationToken.None);

        Assert.Equal(3, manifests.Count);
        PackageManifest frontend = manifests.First(manifest => manifest.Key == "frontend");
        PackageManifest testing = manifests.First(manifest => manifest.Key == "testing");
        PackageManifest backend = manifests.First(manifest => manifest.Key == "backend");

        Assert.Equal("@webstir-io/webstir-frontend", frontend.PackageName);
        Assert.Equal("@webstir-io/webstir-testing", testing.PackageName);
        Assert.Equal("@webstir-io/webstir-backend", backend.PackageName);
        Assert.True(frontend.IsEnabled);
        Assert.True(testing.IsEnabled);
        Assert.True(backend.IsEnabled);
    }

    [Fact]
    public async Task PackageMetadataServiceExplicitSelectionIncludesBackendAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: true, includeBackend: true);

        StubRepositoryDiffService diff = new();
        PackageMetadataService service = new(diff, NullLogger<PackageMetadataService>.Instance);

        IReadOnlyList<PackageManifest> manifests = await service
            .ResolveAsync(
                workspace.RepositoryRoot,
                PackageSelection.Explicit(new[] { "frontend" }),
                sinceReference: null,
                CancellationToken.None);

        Assert.Single(manifests);
        Assert.Equal("frontend", manifests[0].Key);

        manifests = await service
            .ResolveAsync(
                workspace.RepositoryRoot,
                PackageSelection.Explicit(new[] { "backend" }),
                sinceReference: null,
                CancellationToken.None);

        Assert.Single(manifests);
        Assert.Equal("backend", manifests[0].Key);
    }

    [Fact]
    public async Task PackageMetadataServiceDetectsChangedPackagesAsync()
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
                CancellationToken.None);

        Assert.Single(manifests);
        Assert.Equal("frontend", manifests[0].Key);
        Assert.Equal(workspace.RepositoryRoot, diff.LastRepositoryRoot);
        Assert.NotNull(diff.LastOptions);
        Assert.True(diff.LastOptions!.IncludeUntracked);

        diff.NextResult = new RepositoryDiffResult(new[]
        {
            Path.Combine("Framework", "Testing", "spec.ts")
        });

        manifests = await service
            .ResolveAsync(
                workspace.RepositoryRoot,
                PackageSelection.ChangedPackages,
                sinceReference: "HEAD~1",
                CancellationToken.None);

        Assert.Single(manifests);
        Assert.Equal("testing", manifests[0].Key);
        Assert.NotNull(diff.LastOptions);
        Assert.Equal("HEAD~1", diff.LastOptions!.SinceRef);
        Assert.False(diff.LastOptions.IncludeUntracked);
    }

    [Fact]
    public async Task PackageMetadataServiceUpdatesVersionsAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: false);

        StubRepositoryDiffService diff = new();
        PackageMetadataService service = new(diff, NullLogger<PackageMetadataService>.Instance);

        PackageManifest manifest = (await service
            .GetPackagesAsync(workspace.RepositoryRoot, CancellationToken.None)).Single();

        SemanticVersion version = SemanticVersion.Parse("2.0.1");

        await service.UpdatePackageVersionAsync(manifest, version, dryRun: false, CancellationToken.None);

        JsonDocument packageJson = JsonDocument.Parse(await File.ReadAllTextAsync(manifest.PackageJsonPath));
        Assert.Equal("2.0.1", packageJson.RootElement.GetProperty("version").GetString());

        Assert.NotNull(manifest.PackageLockPath);
        JsonDocument packageLock = JsonDocument.Parse(await File.ReadAllTextAsync(manifest.PackageLockPath!));
        Assert.Equal("2.0.1", packageLock.RootElement.GetProperty("version").GetString());
        Assert.Equal(
            "2.0.1",
            packageLock.RootElement.GetProperty("packages").GetProperty(string.Empty).GetProperty("version").GetString());
    }

    [Fact]
    public async Task PackageMetadataServiceHonorsDryRunAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: false);

        StubRepositoryDiffService diff = new();
        PackageMetadataService service = new(diff, NullLogger<PackageMetadataService>.Instance);

        PackageManifest manifest = (await service
            .GetPackagesAsync(workspace.RepositoryRoot, CancellationToken.None)).Single();

        string packageJsonBefore = await File.ReadAllTextAsync(manifest.PackageJsonPath);
        string? packageLockBefore = manifest.PackageLockPath is null
            ? null
            : await File.ReadAllTextAsync(manifest.PackageLockPath);

        await service.UpdatePackageVersionAsync(manifest, SemanticVersion.Parse("3.4.5"), dryRun: true, CancellationToken.None);

        string packageJsonAfter = await File.ReadAllTextAsync(manifest.PackageJsonPath);
        Assert.Equal(packageJsonBefore, packageJsonAfter);

        if (manifest.PackageLockPath is not null)
        {
            string packageLockAfter = await File.ReadAllTextAsync(manifest.PackageLockPath);
            Assert.Equal(packageLockBefore, packageLockAfter);
        }
    }

    [Fact]
    public async Task RepositoryDiffServiceParsesOutputAsync()
    {
        string repositoryRoot = RepositoryRootLocator.Resolve();

        FakeProcessRunner runner = new()
        {
            OnRun = request =>
            {
                Assert.Equal("git", request.FileName);
                Assert.Contains("status --porcelain=1 --untracked-files=all", request.Arguments, StringComparison.Ordinal);
                return new ProcessResult
                {
                    ExitCode = 0,
                    StandardOutput = " M Framework/Frontend/package.json\nA  Framework/Testing/new.ts\nR  Framework/Frontend/old.ts -> Framework/Frontend/new.ts\n?? Framework/Testing/untracked.js\n",
                    StandardError = string.Empty,
                    Duration = TimeSpan.Zero,
                    IsExitCodeAccepted = true
                };
            }
        };

        RepositoryDiffService service = new(runner, NullLogger<RepositoryDiffService>.Instance);
        RepositoryDiffResult result = await service
            .GetStatusAsync(repositoryRoot, new RepositoryDiffOptions(), CancellationToken.None);

        Assert.True(result.HasChanges);
        Assert.Equal(4, result.Paths.Count);
        Assert.Contains("Framework/Frontend/package.json", result.Paths);
        Assert.Contains("Framework/Testing/new.ts", result.Paths);
        Assert.Contains("Framework/Frontend/new.ts", result.Paths);
        Assert.Contains("Framework/Testing/untracked.js", result.Paths);
    }

    [Fact]
    public async Task RepositoryDiffServiceThrowsOnFailureAsync()
    {
        string repositoryRoot = RepositoryRootLocator.Resolve();

        FakeProcessRunner runner = new()
        {
            OnRun = _ => new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = string.Empty,
                StandardError = "fatal: not a git repository",
                Duration = TimeSpan.Zero,
                IsExitCodeAccepted = false
            }
        };

        RepositoryDiffService service = new(runner, NullLogger<RepositoryDiffService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetStatusAsync(repositoryRoot, new RepositoryDiffOptions(), CancellationToken.None));
    }

    [Fact]
    public async Task PackagePublishValidatorRequiresTokensAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: false);

        FakePackageMetadataService metadata = new([workspace.FrontendManifest!]);
        FakeProcessRunner runner = new();
        PackagePublishValidator validator = new(metadata, runner, NullLogger<PackagePublishValidator>.Instance);

        string? originalToken = Environment.GetEnvironmentVariable("NPM_TOKEN");
        Environment.SetEnvironmentVariable("NPM_TOKEN", null);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await validator.ValidateAsync(
                    workspace.RepositoryRoot,
                    PackageSelection.AllPackages,
                    sinceReference: null,
                    CancellationToken.None));

            Assert.Empty(runner.Requests);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NPM_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task PackagePublishValidatorValidatesRegistriesAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: true, testingEnabled: false);

        FakePackageMetadataService metadata = new([workspace.FrontendManifest!]);
        FakeProcessRunner runner = new()
        {
            OnRun = request =>
            {
                Assert.Equal("npm", request.FileName);
                Assert.Contains("ping", request.Arguments, StringComparison.Ordinal);
                Assert.Contains("https://registry.npmjs.org", request.Arguments, StringComparison.Ordinal);
                return new ProcessResult
                {
                    ExitCode = 0,
                    StandardOutput = "pong",
                    StandardError = string.Empty,
                    Duration = TimeSpan.Zero,
                    IsExitCodeAccepted = true
                };
            }
        };

        PackagePublishValidator validator = new(metadata, runner, NullLogger<PackagePublishValidator>.Instance);

        string? originalToken = Environment.GetEnvironmentVariable("NPM_TOKEN");
        Environment.SetEnvironmentVariable("NPM_TOKEN", "fake-token");

        try
        {
            await validator.ValidateAsync(
                workspace.RepositoryRoot,
                PackageSelection.AllPackages,
                sinceReference: null,
                CancellationToken.None);

            Assert.Single(runner.Requests);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NPM_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task PackagePublishValidatorSkipsWhenNoPublishableAsync()
    {
        using TestWorkspace workspace = TestWorkspace.WithPackages(frontendEnabled: false, testingEnabled: false, includeBackend: true);

        FakePackageMetadataService metadata = new([workspace.BackendManifest!]);
        FakeProcessRunner runner = new();
        PackagePublishValidator validator = new(metadata, runner, NullLogger<PackagePublishValidator>.Instance);

        await validator.ValidateAsync(
            workspace.RepositoryRoot,
            PackageSelection.AllPackages,
            sinceReference: null,
            CancellationToken.None);

        Assert.Empty(runner.Requests);
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
        public List<ProcessSpec> Requests { get; } = new();

        public Func<ProcessSpec, ProcessResult>? OnRun
        {
            get; set;
        }

        public Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
        {
            Requests.Add(spec);
            ProcessResult result = OnRun is null
                ? new ProcessResult
                {
                    ExitCode = 0,
                    StandardOutput = string.Empty,
                    StandardError = string.Empty,
                    Duration = TimeSpan.Zero,
                    IsExitCodeAccepted = true
                }
                : OnRun(spec);

            return Task.FromResult(result);
        }

        public Task<IProcessHandle> StartAsync(ProcessSpec spec, CancellationToken cancellationToken = default) => throw new NotImplementedException();
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

        public static TestWorkspace WithPackages(bool frontendEnabled, bool testingEnabled, bool includeBackend = false, bool backendEnabled = true)
        {
            string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "webstir-tests", "package-automation", Guid.NewGuid().ToString("N"))).FullName;
            string frameworkRoot = Path.Combine(root, "Framework");
            Directory.CreateDirectory(frameworkRoot);

            PackageManifest? frontend = frontendEnabled
                ? CreateManifest(frameworkRoot, "Frontend", "@webstir-io/webstir-frontend", version: "1.0.0", enabled: true)
                : null;
            PackageManifest? testing = testingEnabled
                ? CreateManifest(frameworkRoot, "Testing", "@webstir-io/webstir-testing", version: "1.0.0", enabled: true)
                : null;
            PackageManifest? backend = includeBackend
                ? CreateManifest(frameworkRoot, "Backend", "@webstir-io/webstir-backend", version: "1.0.0", enabled: backendEnabled)
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
