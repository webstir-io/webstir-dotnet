using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Engine;
using Tester.Infrastructure;
using Xunit;

namespace Tester.Workflows.Install;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class InstallWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

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

        string command = $"{Commands.Install} {ProjectOptions.ProjectName} {workspaceName} {InstallOptions.DryRun} {InstallOptions.PackageManager}=pnpm@9.0.0";
        Tester.Infrastructure.ProcessRunner.ProcessResult result = context.Run(command, context.OutPath, timeoutMs: 60000);

        Assert.Contains("pnpm@9.0.0", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void DryRunLogsShortPackageManagerOption()
    {
        TestCaseContext context = _fixture.Context;
        string workspace = WorkspaceManager.CreateSeedWorkspace(context, "install-override-short");
        string workspaceName = Path.GetFileName(workspace);

        ForceFrontendSpecDrift(workspace, "workspace:*");

        string command = $"{Commands.Install} {ProjectOptions.ProjectName} {workspaceName} {InstallOptions.DryRun} {InstallOptions.PackageManagerShort} yarn";
        Tester.Infrastructure.ProcessRunner.ProcessResult result = context.Run(command, context.OutPath, timeoutMs: 60000);

        Assert.Contains("yarn install", result.Output, StringComparison.OrdinalIgnoreCase);
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
}
