using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;

namespace Tester.Workflows.Publish;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class SsgPublishWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public SsgPublishWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void PublishDefaultsToSsgFrontendModeWhenWorkspaceModeIsSsg()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping publish tests: framework packages not available (set NPM_TOKEN).");
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-publish-ssg-default";
        string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        SetWorkspaceMode(seedDir, "ssg");

        string distRoot = Path.Combine(seedDir, Folders.Dist);
        if (Directory.Exists(distRoot))
        {
            Directory.Delete(distRoot, recursive: true);
        }

        ProcessResult result = context.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 30000);

        Assert.Equal(0, result.ExitCode);
        context.AssertNoCompilationErrors(result);

        string rootIndex = Path.Combine(seedDir, Folders.Dist, Folders.Frontend, Files.IndexHtml);
        string homeManifest = Path.Combine(seedDir, Folders.Dist, Folders.Frontend, Folders.Home, Files.ManifestJson);

        Assert.True(File.Exists(rootIndex), $"Expected SSG root alias at {rootIndex}");
        Assert.True(File.Exists(homeManifest), $"Expected SSG home assets manifest at {homeManifest}");
    }

    private static void SetWorkspaceMode(string workspaceRoot, string mode)
    {
        string packageJsonPath = Path.Combine(workspaceRoot, Files.PackageJson);
        string json = File.ReadAllText(packageJsonPath);
        JsonObject root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException($"Unable to parse {packageJsonPath} as JSON object.");

        JsonObject webstir = root["webstir"] as JsonObject ?? new JsonObject();
        webstir["mode"] = mode;
        root["webstir"] = webstir;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        File.WriteAllText(packageJsonPath, root.ToJsonString(options));
    }
}
