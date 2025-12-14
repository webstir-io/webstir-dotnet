using System;
using System.IO;
using Engine;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;

namespace Tester.Workflows.Repair;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class RepairWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public RepairWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void RepairRestoresMissingScaffoldFiles()
    {
        TestCaseContext context = _fixture.Context;
        string workspace = WorkspaceManager.CreateSeedWorkspace(context, "seed-repair");

        string appHtmlPath = Path.Combine(workspace, Folders.Src, Folders.Frontend, Folders.App, "app.html");
        if (File.Exists(appHtmlPath))
        {
            File.Delete(appHtmlPath);
        }

        Assert.False(File.Exists(appHtmlPath), "Expected app.html to be deleted before repair.");

        ProcessResult result = context.Run($"{Commands.Repair} \"{workspace}\"", Paths.OutPath, timeoutMs: 20000);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(appHtmlPath), "Expected repair to restore src/frontend/app/app.html.");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void RepairDryRunDoesNotWriteFiles()
    {
        TestCaseContext context = _fixture.Context;
        string workspace = WorkspaceManager.CreateSeedWorkspace(context, "seed-repair-dry-run");

        string appHtmlPath = Path.Combine(workspace, Folders.Src, Folders.Frontend, Folders.App, "app.html");
        if (File.Exists(appHtmlPath))
        {
            File.Delete(appHtmlPath);
        }

        Assert.False(File.Exists(appHtmlPath), "Expected app.html to be deleted before repair dry-run.");

        ProcessResult result = context.Run(
            $"{Commands.Repair} {RepairOptions.DryRun} \"{workspace}\"",
            Paths.OutPath,
            timeoutMs: 20000);

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(appHtmlPath), "Expected repair --dry-run not to write src/frontend/app/app.html.");
        Assert.Contains("Would restore", result.StandardOutput ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
