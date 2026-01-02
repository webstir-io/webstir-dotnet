using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;

namespace Tester.Workflows.Install;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class InstallWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;
    private const string BackendPackageName = "@webstir-io/webstir-backend";

    public InstallWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void DryRunLogsPackageManagerOverride()
    {
        TestCaseContext context = _fixture.Context;
        string workspace = WorkspaceManager.CreateSeedWorkspace(context, "install-override");
        string workspaceName = Path.GetFileName(workspace);

        ForceFrontendSpecDrift(workspace, "file:../override");

        string command = $"{Commands.Install} {ProjectOptions.ProjectName} {workspaceName} {InstallOptions.DryRun} {InstallOptions.PackageManager}=pnpm@10.5.2";
        ProcessResult result = context.Run(command, context.OutPath, timeoutMs: 60000);

        Assert.Contains("pnpm@10.5.2", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void DryRunDoesNotReaddBackendDependencyForSpaMode()
    {
        TestCaseContext context = _fixture.Context;
        string workspace = WorkspaceManager.CreateSeedWorkspace(context, "install-spa");
        string workspaceName = Path.GetFileName(workspace);

        ConfigureSpaMode(workspace);

        string command = $"{Commands.Install} {ProjectOptions.ProjectName} {workspaceName} {InstallOptions.DryRun}";
        ProcessResult result = context.Run(command, context.OutPath, timeoutMs: 60000);

        Assert.Equal(0, result.ExitCode);
        AssertBackendDependencyAbsent(workspace);
    }

    private static void ForceFrontendSpecDrift(string workspacePath, string specifier)
    {
        string packageJsonPath = Path.Combine(workspacePath, Files.PackageJson);
        JsonNode? root = JsonNode.Parse(File.ReadAllText(packageJsonPath));
        if (root is null)
        {
            throw new InvalidOperationException("Unable to parse workspace package.json.");
        }

        JsonObject dependencies = root["dependencies"]?.AsObject() ?? [];
        dependencies["@webstir-io/webstir-frontend"] = specifier;
        root["dependencies"] = dependencies;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        File.WriteAllText(packageJsonPath, root.ToJsonString(options) + Environment.NewLine);
    }

    private static void ConfigureSpaMode(string workspacePath)
    {
        string packageJsonPath = Path.Combine(workspacePath, Files.PackageJson);
        JsonNode? rootNode = JsonNode.Parse(File.ReadAllText(packageJsonPath));
        if (rootNode is not JsonObject root)
        {
            throw new InvalidOperationException("Unable to parse workspace package.json.");
        }

        JsonObject webstir = root["webstir"]?.AsObject() ?? [];
        webstir["mode"] = "spa";
        root["webstir"] = webstir;

        JsonObject dependencies = root["dependencies"]?.AsObject() ?? [];
        dependencies.Remove(BackendPackageName);
        root["dependencies"] = dependencies;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        File.WriteAllText(packageJsonPath, root.ToJsonString(options) + Environment.NewLine);

        string backendPath = Path.Combine(workspacePath, Folders.Src, Folders.Backend);
        if (Directory.Exists(backendPath))
        {
            Directory.Delete(backendPath, recursive: true);
        }
    }

    private static void AssertBackendDependencyAbsent(string workspacePath)
    {
        string packageJsonPath = Path.Combine(workspacePath, Files.PackageJson);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        JsonElement dependencies = document.RootElement.GetProperty("dependencies");

        Assert.False(
            dependencies.TryGetProperty(BackendPackageName, out _),
            "Backend dependency should not be re-added for SPA mode.");
    }
}
