using System;
using System.IO;
using System.Text.Json;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;
using Xunit.Sdk;

namespace Tester.Workflows.Enable;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class EnableWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public EnableWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void EnableGhDeploymentsScaffoldsWorkflow()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping enable workflow: framework packages not available (set NPM_TOKEN).");
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);

        string projectName = "seed-gh-deploy";
        string projectDir = Path.Combine(testDir, projectName);
        if (Directory.Exists(projectDir))
        {
            try
            {
                Directory.Delete(projectDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures; init overwrites the directory.
            }
        }

        ProcessResult initResult = context.Run(
            $"{Commands.Init} {InitModes.Ssg} {projectName}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, initResult.ExitCode);

        ProcessResult enableResult = context.Run(
            $"{Commands.Enable} gh-deploy ./{projectName}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, enableResult.ExitCode);

        string deployScriptPath = Path.Combine(projectDir, Folders.Utils, Files.DeployGhPagesScript);
        Assert.True(File.Exists(deployScriptPath), "deploy-gh-pages.sh missing");

        string workflowPath = Path.Combine(projectDir, Folders.Github, Folders.Workflows, Files.DeployGhPagesWorkflow);
        Assert.True(File.Exists(workflowPath), "GitHub Pages workflow missing");

        string frontendConfigPath = Path.Combine(projectDir, Folders.Src, Folders.Frontend, "frontend.config.json");
        Assert.True(File.Exists(frontendConfigPath), "frontend.config.json missing");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(frontendConfigPath));
        JsonElement publish = document.RootElement.GetProperty("publish");
        string? basePath = publish.GetProperty("basePath").GetString();
        Assert.Equal("/" + projectName, basePath);
    }
}
