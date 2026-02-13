using System;
using System.IO;
using Tester.Helpers;
using Engine;
using Tester.Infrastructure;
using Xunit;
using Xunit.Sdk;

namespace Tester.Pipelines.Css;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class CssPipelineTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public CssPipelineTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void CssIsMinified()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping CSS pipeline tests: framework packages not available (set NPM_TOKEN).");
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(_fixture.Context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string expectedCssPath = ResolveCssPath(homePage);

        Assert.True(File.Exists(expectedCssPath), "CSS file missing in dist (checked via manifest)");

        string css = File.ReadAllText(expectedCssPath);
        bool hasNonImportantBlockComment = false;
        for (int i = 0; i < css.Length - 3; i++)
        {
            if (css[i] == '/' && css[i + 1] == '*')
            {
                if (i + 2 >= css.Length || css[i + 2] != '!')
                {
                    hasNonImportantBlockComment = true;
                    break;
                }
            }
        }

        Assert.False(hasNonImportantBlockComment, "Client CSS should not contain non-important block comments after minification");
        Assert.DoesNotContain("  ", css);
        Assert.DoesNotContain("\n\n", css);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void CssPrecompressedAreSmaller()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping CSS pipeline tests: framework packages not available (set NPM_TOKEN).");
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.PrecompressionEnabled(_fixture.Context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string expectedCssPath = ResolveCssPath(homePage);

        Assert.True(File.Exists(expectedCssPath), "CSS file missing in dist (checked via manifest)");

        FileInfo original = new(expectedCssPath);
        FileInfo br = new(expectedCssPath + FileExtensions.Br);

        Assert.True(br.Exists, ".css.br variant missing next to CSS");
        Assert.True(br.Length < original.Length, "Brotli output should be smaller than the original CSS");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void CssSeedSnapshotMatches()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(_fixture.Context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string expectedCssPath = ResolveCssPath(homePage);

        Assert.True(File.Exists(expectedCssPath), "CSS file missing in dist (checked via manifest)");
        string actual = File.ReadAllText(expectedCssPath);

        string snapshotPath = Path.Combine(Paths.RepositoryRoot, "Tester", "Pipelines", "Css", "__snapshots__", "seed-home-index.css");
        Assert.True(File.Exists(snapshotPath), $"Snapshot file not found: {snapshotPath}");
        string expected = File.ReadAllText(snapshotPath);

        static string Normalize(string s) => s.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    private static string ResolveCssPath(HtmlPageResult page)
    {
        string directory = page.DirectoryPath;
        string? manifestCss = page.Manifest.Css;
        return !string.IsNullOrWhiteSpace(manifestCss)
            ? Path.Combine(directory, manifestCss!)
            : Path.Combine(directory, $"{Files.Index}{FileExtensions.Css}");
    }
}
