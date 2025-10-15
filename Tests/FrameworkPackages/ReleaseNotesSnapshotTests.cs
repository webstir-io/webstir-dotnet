using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Framework.Services;
using Framework.Utilities;
using Framework.Commands;
using Tests.Framework;

namespace Tests.FrameworkPackages;

public sealed class ReleaseNotesSnapshotTests : TestSuite
{
    public override string Name => "Framework release notes snapshots";

    public override async Task<TestResult[]> RunAsync()
    {
        List<(string TestName, Func<Task> TestAction)> tests =
        [
            ("release notes group commits by type", VerifySectionGroupingAsync),
            ("release notes fall back when commits missing", VerifyFallbackSectionAsync)
        ];

        return await RunTestsAsync(tests).ConfigureAwait(false);
    }

    private static async Task VerifySectionGroupingAsync()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();

        PackageManifest manifest = repository.CreateManifest(
            "frontend",
            "@webstir-io/webstir-frontend",
            SemanticVersion.Parse("1.0.0"));

        PackageBumpEntry entry = PackageBumpEntry.Create(
            manifest,
            SemanticVersionBump.Major,
            new[]
            {
                "feat: add hero section",
                "fix: adjust padding",
                "docs: update readme",
                "test: add coverage",
                "perf: speed up build",
                "refactor: restructure layout",
                "chore: dependency upgrades",
                "style: align button",
                "BREAKING CHANGE: drop legacy support"
            });

        PackageBumpSummary summary = new(
            HasPackages: true,
            TargetVersion: SemanticVersion.Parse("1.1.0"),
            AppliedBump: SemanticVersionBump.Major,
            UsedHeuristics: true,
            ExplicitVersionSpecified: false,
            Entries: new[] { entry },
            DryRun: false);

        ReleaseNotesService service = new();
        ReleaseNotesResult result = await service
            .WriteAsync(repository.RootPath, summary, CancellationToken.None)
            .ConfigureAwait(false);

        Assert.IsTrue(result.HasDocuments, "Expected release notes document to be created.");
        ReleaseNotesDocument document = result.Documents[0];

        string contents = await File.ReadAllTextAsync(document.FilePath).ConfigureAwait(false);
        contents = contents.ReplaceLineEndings("\n");

        string expected = string.Join(
                "\n",
                new[]
                {
                    "# @webstir-io/webstir-frontend 1.1.0",
                    string.Empty,
                    "## Breaking Changes",
                    string.Empty,
                    "- drop legacy support",
                    string.Empty,
                    "## Features",
                    string.Empty,
                    "- add hero section",
                    string.Empty,
                    "## Fixes",
                    string.Empty,
                    "- adjust padding",
                    string.Empty,
                    "## Performance",
                    string.Empty,
                    "- speed up build",
                    string.Empty,
                    "## Refactors",
                    string.Empty,
                    "- restructure layout",
                    string.Empty,
                    "## Documentation",
                    string.Empty,
                    "- update readme",
                    string.Empty,
                    "## Testing",
                    string.Empty,
                    "- add coverage",
                    string.Empty,
                    "## Chores",
                    string.Empty,
                    "- dependency upgrades",
                    string.Empty,
                    "## Style",
                    string.Empty,
                    "- align button",
                    string.Empty
                }) + "\n";

        Assert.AreEqual(expected, contents, "Release notes snapshot mismatch.");
    }

    private static async Task VerifyFallbackSectionAsync()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();

        PackageManifest manifest = repository.CreateManifest(
            "frontend",
            "@webstir-io/webstir-frontend",
            SemanticVersion.Parse("2.0.0"));

        PackageBumpEntry entry = PackageBumpEntry.Create(
            manifest,
            null,
            Array.Empty<string>());

        PackageBumpSummary summary = new(
            HasPackages: true,
            TargetVersion: SemanticVersion.Parse("2.0.1"),
            AppliedBump: SemanticVersionBump.Patch,
            UsedHeuristics: false,
            ExplicitVersionSpecified: false,
            Entries: new[] { entry },
            DryRun: false);

        ReleaseNotesService service = new();
        ReleaseNotesResult result = await service
            .WriteAsync(repository.RootPath, summary, CancellationToken.None)
            .ConfigureAwait(false);

        ReleaseNotesDocument document = result.Documents[0];
        string contents = await File.ReadAllTextAsync(document.FilePath).ConfigureAwait(false);
        contents = contents.ReplaceLineEndings("\n");

        string expected = string.Join(
                "\n",
                new[]
                {
                    "# @webstir-io/webstir-frontend 2.0.1",
                    string.Empty,
                    "## Other",
                    string.Empty,
                    "- Version bump only (no notable commits detected).",
                    string.Empty
                }) + "\n";

        Assert.AreEqual(expected, contents, "Release notes fallback snapshot mismatch.");
    }

    private sealed class TemporaryRepository : IDisposable
    {
        private TemporaryRepository(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath
        {
            get;
        }

        public static TemporaryRepository Create()
        {
            string root = Directory.CreateDirectory(Path.Combine(Paths.OutPath, "release-notes", Guid.NewGuid().ToString("N"))).FullName;
            Directory.CreateDirectory(Path.Combine(root, "Framework", "Packaging"));
            Directory.CreateDirectory(Path.Combine(root, "Framework", "Frontend"));
            return new TemporaryRepository(root);
        }

        public PackageManifest CreateManifest(string key, string packageName, SemanticVersion version)
        {
            string packageDirectory = Path.Combine(RootPath, "Framework", key.Equals("frontend", StringComparison.OrdinalIgnoreCase) ? "Frontend" : key);
            Directory.CreateDirectory(packageDirectory);

            string packageJsonPath = Path.Combine(packageDirectory, "package.json");
            File.WriteAllText(packageJsonPath, "{\"name\": \"" + packageName + "\", \"version\": \"" + version + "\"}");

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
                PackageLockPath: null,
                version,
                identifiers,
                IsEnabled: true);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch
            {
                // ignore clean-up failures in test harness
            }
        }
    }
}
