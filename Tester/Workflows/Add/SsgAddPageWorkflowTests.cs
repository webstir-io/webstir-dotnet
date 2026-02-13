using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;

namespace Tester.Workflows.Add;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class SsgAddPageWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public SsgAddPageWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void AddPageDefaultsToSsgScaffoldWhenWorkspaceModeIsSsg()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping add-page tests: framework packages not available (set NPM_TOKEN).");
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-add-page-ssg-default";
        string seedDir = WorkspaceManager.CreateSeedWorkspace(context, projectName);

        SetWorkspaceMode(seedDir, "ssg");

        ProcessResult result = context.Run(
            $"{Commands.AddPage} about {ProjectOptions.ProjectName} {projectName}",
            testDir,
            timeoutMs: 15000);

        Assert.Equal(0, result.ExitCode);

        string pageDir = Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.Pages, "about");
        string htmlPath = Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Html}");
        string cssPath = Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Css}");
        string tsPath = Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Ts}");

        Assert.True(File.Exists(htmlPath), "index.html not created");
        Assert.True(File.Exists(cssPath), "index.css not created");
        Assert.False(File.Exists(tsPath), "index.ts should not be created in ssg scaffolds");

        string html = File.ReadAllText(htmlPath);
        Assert.DoesNotContain("<script type=\"module\"", html, StringComparison.OrdinalIgnoreCase);
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

