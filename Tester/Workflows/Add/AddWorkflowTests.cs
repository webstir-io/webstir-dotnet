using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Engine;
using Framework.Packaging;
using Tester.Infrastructure;
using Utilities.Process;
using Xunit;

namespace Tester.Workflows.Add;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class AddWorkflowTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public AddWorkflowTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddPageCreatesFiles()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        ProcessResult result = context.Run(
            $"{Commands.AddPage} about {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string pageDir = Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.Pages, "about");
        Assert.True(File.Exists(Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Html}")), "index.html not created");
        Assert.True(File.Exists(Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Css}")), "index.css not created");
        Assert.True(File.Exists(Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Ts}")), "index.ts not created");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddTestScaffoldsFile()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        ProcessResult result = context.Run(
            $"{Commands.AddTest} frontend/pages/home/home {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string expectedTest = Path.Combine(seedDir, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home, Folders.Tests, "home.test.ts");
        Assert.True(File.Exists(expectedTest), $"Test file not created at {expectedTest}");

        string packageJsonPath = Path.Combine(seedDir, Files.PackageJson);
        Assert.True(File.Exists(packageJsonPath), $"{Files.PackageJson} not found");

        using JsonDocument packageManifest = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        JsonElement dependencies = packageManifest.RootElement.GetProperty("dependencies");
        string expectedSpecifier = FrameworkPackageCatalog.Testing.WorkspaceSpecifier;
        string actualSpecifier = dependencies.GetProperty("@webstir-io/webstir-testing").GetString() ?? string.Empty;
        Assert.Equal(expectedSpecifier, actualSpecifier);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddJobCreatesFilesAndManifestEntry()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        const string jobName = "cleanup";
        ProcessResult result = context.Run(
            $"{Commands.AddJob} {jobName} {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string jobFile = Path.Combine(seedDir, Folders.Src, Folders.Backend, "jobs", jobName, $"{Files.Index}{FileExtensions.Ts}");
        Assert.True(File.Exists(jobFile), $"Job file not created at {jobFile}");

        string packageJsonPath = Path.Combine(seedDir, Files.PackageJson);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        JsonElement root = doc.RootElement;
        Assert.True(root.TryGetProperty("webstir", out JsonElement webstir), "package.json missing 'webstir'");
        Assert.True(webstir.TryGetProperty("moduleManifest", out JsonElement moduleManifest), "package.json missing 'webstir.moduleManifest'");
        Assert.True(moduleManifest.TryGetProperty("jobs", out JsonElement jobs), "package.json missing 'webstir.moduleManifest.jobs'");
        bool found = false;
        foreach (JsonElement j in jobs.EnumerateArray())
        {
            if (j.TryGetProperty("name", out JsonElement n) && string.Equals(n.GetString(), jobName, StringComparison.Ordinal))
            {
                found = true;
                break;
            }
        }
        Assert.True(found, $"Job '{jobName}' not found in manifest.");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddJobWithScheduleWritesManifest()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        const string jobName = "cleanup2";
        const string schedule = "0 12 * * *"; // noon daily
        ProcessResult result = context.Run(
            $"{Commands.AddJob} {jobName} --schedule \"{schedule}\" {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string packageJsonPath = Path.Combine(seedDir, Files.PackageJson);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        JsonElement root = doc.RootElement;
        Assert.True(root.TryGetProperty("webstir", out JsonElement webstir), "package.json missing 'webstir'");
        Assert.True(webstir.TryGetProperty("moduleManifest", out JsonElement moduleManifest), "package.json missing 'webstir.moduleManifest'");
        Assert.True(moduleManifest.TryGetProperty("jobs", out JsonElement jobs), "package.json missing 'webstir.moduleManifest.jobs'");
        bool found = false;
        foreach (JsonElement j in jobs.EnumerateArray())
        {
            string? name = j.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
            string? sched = j.TryGetProperty("schedule", out JsonElement s) ? s.GetString() : null;
            if (string.Equals(name, jobName, StringComparison.Ordinal))
            {
                found = true;
                Assert.Equal(schedule, sched);
                break;
            }
        }
        Assert.True(found, $"Job '{jobName}' not found in manifest.");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddJobWithMetadataWritesDescriptionAndPriority()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        const string jobName = "priority-job";
        const string description = "Nightly cleanup";
        const string priority = "5";
        ProcessResult result = context.Run(
            $"{Commands.AddJob} {jobName} --description \"{description}\" --priority {priority} {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string packageJsonPath = Path.Combine(seedDir, Files.PackageJson);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        JsonElement jobs = doc.RootElement.GetProperty("webstir").GetProperty("moduleManifest").GetProperty("jobs");
        JsonElement? jobElement = jobs.EnumerateArray()
            .FirstOrDefault(j => string.Equals(j.GetProperty("name").GetString(), jobName, StringComparison.Ordinal));

        Assert.True(jobElement.HasValue, $"Job '{jobName}' not found in manifest.");
        Assert.Equal(description, jobElement!.Value.GetProperty("description").GetString());
        Assert.Equal(int.Parse(priority, CultureInfo.InvariantCulture), jobElement.Value.GetProperty("priority").GetInt32());
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddJobWithInvalidScheduleFails()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);

        const string jobName = "invalid-schedule";
        ProcessResult result = context.Run(
            $"{Commands.AddJob} {jobName} --schedule \"every day\" {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.NotEqual(0, result.ExitCode);
        string combined = (result.StandardOutput + result.StandardError) ?? string.Empty;
        Assert.Contains("Invalid --schedule value", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddRouteWithFastifyScaffoldsAndRegisters()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        // Create a minimal fastify server file that our workflow can patch
        string serverDir = Path.Combine(seedDir, Folders.Src, Folders.Backend, "server");
        Directory.CreateDirectory(serverDir);
        string fastifyFile = Path.Combine(serverDir, "fastify.ts");
        File.WriteAllText(fastifyFile, """
import Fastify from 'fastify';

export async function start() {
  const app = Fastify({ logger: false });
  app.get('/api/health', async () => ({ ok: true }));
}
""");

        const string routeName = "profile";
        ProcessResult result = context.Run(
            $"{Commands.AddRoute} {routeName} --fastify {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string routeFile = Path.Combine(serverDir, "routes", $"{routeName}.ts");
        Assert.True(File.Exists(routeFile), $"Route file not created at {routeFile}");

        string patched = File.ReadAllText(fastifyFile);
        Assert.Contains("import { register as registerProfile } from './routes/profile';", patched, StringComparison.Ordinal);
        Assert.Contains("registerProfile(app);", patched, StringComparison.Ordinal);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void HelpShowsAddRouteAndAddJob()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);

        ProcessResult routeHelp = context.Run($"{Commands.Help} {Commands.AddRoute}", testDir, timeoutMs: 8000);
        Assert.Equal(0, routeHelp.ExitCode);
        Assert.Contains("Add a backend route entry", routeHelp.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--method", routeHelp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--path", routeHelp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--summary", routeHelp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--description", routeHelp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--tags", routeHelp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(ProjectOptions.ProjectName, routeHelp.StandardOutput, StringComparison.Ordinal);

        ProcessResult jobHelp = context.Run($"{Commands.Help} {Commands.AddJob}", testDir, timeoutMs: 8000);
        Assert.Equal(0, jobHelp.ExitCode);
        Assert.Contains("Add a backend job stub", jobHelp.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--schedule", jobHelp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--description", jobHelp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--priority", jobHelp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(ProjectOptions.ProjectName, jobHelp.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddRouteUpdatesManifest()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        const string routeName = "users";
        ProcessResult result = context.Run(
            $"{Commands.AddRoute} {routeName} {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string packageJsonPath = Path.Combine(seedDir, Files.PackageJson);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        JsonElement root = doc.RootElement;
        Assert.True(root.TryGetProperty("webstir", out JsonElement webstir), "package.json missing 'webstir'");
        Assert.True(webstir.TryGetProperty("moduleManifest", out JsonElement moduleManifest), "package.json missing 'webstir.moduleManifest'");
        Assert.True(moduleManifest.TryGetProperty("routes", out JsonElement routes), "package.json missing 'webstir.moduleManifest.routes'");
        bool found = false;
        foreach (JsonElement r in routes.EnumerateArray())
        {
            string method = r.TryGetProperty("method", out JsonElement m) ? (m.GetString() ?? string.Empty) : string.Empty;
            string path = r.TryGetProperty("path", out JsonElement p) ? (p.GetString() ?? string.Empty) : string.Empty;
            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) && string.Equals(path, "/api/users", StringComparison.Ordinal))
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Route GET /api/users not found in manifest.");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddRouteWithMetadataWritesManifest()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        const string routeName = "reports";
        const string summary = "List reports";
        const string description = "Returns paginated reports";
        const string tags = "analytics, reports ,analytics";
        ProcessResult result = context.Run(
            $"{Commands.AddRoute} {routeName} --summary \"{summary}\" --description \"{description}\" --tags \"{tags}\" {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string packageJsonPath = Path.Combine(seedDir, Files.PackageJson);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        JsonElement routes = doc.RootElement.GetProperty("webstir").GetProperty("moduleManifest").GetProperty("routes");
        JsonElement? routeElement = routes.EnumerateArray()
            .FirstOrDefault(r => string.Equals(r.GetProperty("path").GetString(), "/api/reports", StringComparison.Ordinal));

        Assert.True(routeElement.HasValue, "Route /api/reports not found in manifest.");
        Assert.Equal(summary, routeElement!.Value.GetProperty("summary").GetString());
        Assert.Equal(description, routeElement.Value.GetProperty("description").GetString());

        JsonElement tagsElement = routeElement.Value.GetProperty("tags");
        string[] capturedTags = tagsElement.EnumerateArray().Select(t => t.GetString() ?? string.Empty).ToArray();
        Assert.Equal(new[] { "analytics", "reports" }, capturedTags);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddRouteWithSchemaFlagsWritesReferences()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);
        string seedDir = Path.Combine(testDir, Folders.Seed);

        const string routeName = "invoices";
        ProcessResult result = context.Run(
            $"{Commands.AddRoute} {routeName} --params-schema AccountParams --query-schema json-schema:AccountQuery@./schemas/account-query.json --body-schema BodySchema --headers-schema HeadersSchema --response-schema ResponseSchema --response-status 201 --response-headers-schema ResponseHeaders {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.Equal(0, result.ExitCode);

        string packageJsonPath = Path.Combine(seedDir, Files.PackageJson);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        JsonElement routes = doc.RootElement.GetProperty("webstir").GetProperty("moduleManifest").GetProperty("routes");
        JsonElement? routeElement = routes.EnumerateArray()
            .FirstOrDefault(r => string.Equals(r.GetProperty("path").GetString(), $"/api/{routeName}", StringComparison.Ordinal));

        Assert.True(routeElement.HasValue, "Route not found in manifest");

        JsonElement input = routeElement!.Value.GetProperty("input");
        Assert.Equal("zod", input.GetProperty("params").GetProperty("kind").GetString());
        Assert.Equal("AccountParams", input.GetProperty("params").GetProperty("name").GetString());

        JsonElement querySchema = input.GetProperty("query");
        Assert.Equal("json-schema", querySchema.GetProperty("kind").GetString());
        Assert.Equal("AccountQuery", querySchema.GetProperty("name").GetString());
        Assert.Equal("./schemas/account-query.json", querySchema.GetProperty("source").GetString());

        Assert.Equal("BodySchema", input.GetProperty("body").GetProperty("name").GetString());
        Assert.Equal("HeadersSchema", input.GetProperty("headers").GetProperty("name").GetString());

        JsonElement output = routeElement.Value.GetProperty("output");
        Assert.Equal("ResponseSchema", output.GetProperty("body").GetProperty("name").GetString());
        Assert.Equal(201, output.GetProperty("status").GetInt32());
        Assert.Equal("ResponseHeaders", output.GetProperty("headers").GetProperty("name").GetString());
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void AddRouteWithInvalidSchemaFlagFails()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        TestCaseContext context = _fixture.Context;
        string testDir = context.OutPath;
        Directory.CreateDirectory(testDir);
        WorkspaceManager.EnsureSeedWorkspaceReady(context);

        ProcessResult result = context.Run(
            $"{Commands.AddRoute} broken --body-schema :invalid {ProjectOptions.ProjectName} {Folders.Seed}",
            testDir,
            timeoutMs: 10000);

        Assert.NotEqual(0, result.ExitCode);
        string combined = (result.StandardOutput + result.StandardError) ?? string.Empty;
        Assert.Contains("Invalid --body-schema value", combined, StringComparison.OrdinalIgnoreCase);
    }
}
