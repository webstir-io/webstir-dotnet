using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Framework.Packaging;
using Tests.Framework;

namespace Tests.PackageInstallers;

internal sealed class RegistryDependencyUpdate : ITestCase
{
    public string Name => "updates frontend dependency to registry specifier";

    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        string workspaceRoot = Path.Combine(context.OutPath, "packages", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            FrameworkPackageMetadata metadata = FrameworkPackageCatalog.Frontend;
            string packageJsonPath = Path.Combine(workspaceRoot, "package.json");
            WritePackageJson(packageJsonPath, metadata.Name, "file:./.tools/legacy.tgz");

            TestWorkspace workspace = new(workspaceRoot);

            PackageEnsureSummary summary = PackageSynchronizer.EnsureAsync(
                workspace,
                logger: null,
                ensureFrontend: preferRegistry => FrontendPackageInstaller.EnsureAsync(workspace, preferRegistry),
                ensureTesting: null,
                includeFrontend: true,
                includeTesting: false,
                autoInstall: false).GetAwaiter().GetResult();

            Assert.IsTrue(summary.InstallRequiredButSkipped, "Install should be required when dependencies change.");
            Assert.IsTrue(summary.Frontend.HasValue, "Frontend summary missing.");

            FrontendPackageEnsureResult frontend = summary.Frontend!.Value;
            Assert.IsTrue(frontend.DependencyUpdated, "DependencyUpdated should be true when specifier changes.");
            Assert.AreEqual(metadata.RegistrySpecifier, ReadDependencySpecifier(packageJsonPath, metadata.Name), "Dependency specifier not updated to registry value.");
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
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

        public string ToolsPath => Path.Combine(root, ".tools");

        public Task RunNpmInstallAsync() => Task.CompletedTask;
    }
}
