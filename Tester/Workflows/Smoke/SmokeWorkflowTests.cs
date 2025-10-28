using System;
using System.IO;
using System.Text.Json;
using Engine;
using Tester.Helpers;
using Tester.Infrastructure;
using Xunit;
using Xunit.Sdk;

namespace Tester.Workflows.Smoke;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class SmokeWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public SmokeWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void SmokeCommandProducesManifestRoutes()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping smoke workflow test: framework packages are unavailable.");
        }

        TestCaseContext context = _fixture.Context;
        ProcessRunner.ProcessResult result = context.Run(
            Commands.Smoke,
            Paths.RepositoryRoot,
            timeoutMs: 120000);

        Assert.False(result.TimedOut, $"{Commands.Smoke} command timed out.");
        Assert.Equal(0, result.ExitCode);

        string manifestPath = Path.Combine(
            Paths.RepositoryRoot,
            "CLI",
            "out",
            "smoke",
            "accounts",
            ".webstir",
            Files.BackendManifestJson);

        Assert.True(File.Exists(manifestPath), $"Backend manifest missing at {manifestPath}");

        using FileStream stream = File.OpenRead(manifestPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("module", out JsonElement moduleElement) || moduleElement.ValueKind != JsonValueKind.Object)
        {
            throw new XunitException("Smoke manifest did not include module metadata.");
        }

        int routeCount = moduleElement.TryGetProperty("routes", out JsonElement routesElement) && routesElement.ValueKind == JsonValueKind.Array
            ? routesElement.GetArrayLength()
            : 0;

        Assert.True(routeCount > 0, "Smoke manifest did not report any route definitions.");
    }
}
