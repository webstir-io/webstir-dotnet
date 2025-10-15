using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Framework.Packaging;
using Tester.Infrastructure;
using Xunit;

namespace Tester.PackageInstallers;

public sealed class PackageInstallerUnitTests
{
    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public async Task TarballDependencyUpdateAsync()
    {
        string workspaceRoot = Path.Combine(Paths.OutPath, "packages", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            FrameworkPackageMetadata metadata = FrameworkPackageCatalog.Frontend;
            string packageJsonPath = Path.Combine(workspaceRoot, "package.json");
            WritePackageJson(packageJsonPath, metadata.Name, "file:./.webstir/legacy.tgz");

            TestWorkspace workspace = new(workspaceRoot);

            PackageEnsureSummary summary = await PackageSynchronizer.EnsureAsync(
                workspace,
                logger: null,
                ensureFrontend: preferRegistry => FrontendPackageInstaller.EnsureAsync(workspace, preferRegistry),
                ensureTesting: null,
                includeFrontend: true,
                includeTesting: false,
                autoInstall: false);

            Assert.True(summary.InstallRequiredButSkipped, "Install should be required when dependencies change.");
            Assert.True(summary.Frontend.HasValue, "Frontend summary missing.");

            FrontendPackageEnsureResult frontend = summary.Frontend!.Value;
            Assert.True(frontend.DependencyUpdated, "DependencyUpdated should be true when specifier changes.");
            string expectedSpecifier = $"file:./.webstir/{metadata.Tarball.FileName}";
            Assert.Equal(expectedSpecifier, ReadDependencySpecifier(packageJsonPath, metadata.Name));

            string tarballPath = Path.Combine(workspaceRoot, ".webstir", metadata.Tarball.FileName);
            Assert.True(File.Exists(tarballPath), $"Tarball not copied to workspace at {tarballPath}.");
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private static void WritePackageJson(string packageJsonPath, string packageName, string specifier)
    {
        JsonObject root = new()
        {
            ["name"] = "workspace",
            ["version"] = "1.0.0",
            ["dependencies"] = new JsonObject
            {
                [packageName] = specifier
            }
        };

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        File.WriteAllText(packageJsonPath, root.ToJsonString(options));
    }

    private static string? ReadDependencySpecifier(string packageJsonPath, string packageName)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        return document.RootElement
            .GetProperty("dependencies")
            .GetProperty(packageName)
            .GetString();
    }

    private sealed class TestWorkspace(string root) : IPackageWorkspace
    {
        public string WorkingPath => root;
        public string NodeModulesPath => Path.Combine(root, "node_modules");
        public string WebstirPath => Path.Combine(root, ".webstir");
        public Task RunNpmInstallAsync() => Task.CompletedTask;
        public Task InstallPackagesAsync(params string[] packageSpecs) => Task.CompletedTask;
    }
}
