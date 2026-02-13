using System;
using System.IO;
using Tester.Helpers;
using Tester.Infrastructure;
using Xunit;
using Xunit.Sdk;
using Engine;

namespace Tester.Pipelines.Html;

[Collection(SeedWorkspaceCollection.CollectionName)]
public sealed class HtmlPipelineTests
{
    private readonly SeedWorkspaceFixture _fixture;

    public HtmlPipelineTests(SeedWorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void HtmlWhitespaceCollapsed()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(_fixture.Context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        string distHtml = homePage.Html;

        Assert.DoesNotContain('\n', distHtml);
        Assert.DoesNotContain('\t', distHtml);
        Assert.True(distHtml.Contains("<style data-critical=\"\">", StringComparison.Ordinal));
        Assert.True(distHtml.Contains("<script type=\"module\" src=\"/pages/home/index", StringComparison.Ordinal));
        string collapsed = homePage.HtmlNormalized
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal);
        Assert.Contains("</main></body></html>", collapsed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void HtmlAttributesAndCommentsOptimized()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(_fixture.Context);
        string distHtml = scenario.GetPage(Folders.Home).Html;

        Assert.DoesNotContain("<!-- head comment should be removed -->", distHtml);
        Assert.DoesNotContain("<!-- body comment should be removed -->", distHtml);
        Assert.True(distHtml.Contains("<button disabled=\"disabled\" class=\"primary\"", StringComparison.Ordinal));
        Assert.True(distHtml.Contains("data-info=\"foo bar\"", StringComparison.Ordinal));
        Assert.True(distHtml.Contains("rel=\"nofollow\"", StringComparison.Ordinal));
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void HtmlMetaPreservation()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(_fixture.Context);
        string distHtml = scenario.GetPage(Folders.Home).Html;

        Assert.Equal(1, CountOccurrences(distHtml, "name=\"viewport\""));
        Assert.True(distHtml.Contains("content=\"page-viewport\"", StringComparison.Ordinal));

        Assert.Equal(1, CountOccurrences(distHtml, "rel=\"canonical\""));
        Assert.True(distHtml.Contains("href=\"/home\"", StringComparison.Ordinal));

        Assert.True(distHtml.Contains("name=\"description\"", StringComparison.Ordinal));
        Assert.True(distHtml.Contains("content=\"page-desc\"", StringComparison.Ordinal));
        Assert.True(distHtml.Contains("property=\"og:title\"", StringComparison.Ordinal));
        Assert.True(distHtml.Contains("content=\"OG Page Title\"", StringComparison.Ordinal));
        Assert.True(distHtml.Contains("data-test=\"head-script\"", StringComparison.Ordinal));
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void HtmlPublishIncludesMetaDescription()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping HTML pipeline tests: framework packages not available (set NPM_TOKEN).");
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.Default(_fixture.Context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);
        _fixture.Context.AssertNoCompilationErrors(scenario.PublishResult);
        Assert.Equal(0, scenario.PublishResult.ExitCode);

        string distHtml = homePage.HtmlNormalized;
        Assert.True(distHtml.Contains("meta name=\"description\"", StringComparison.Ordinal));
        Assert.True(distHtml.Contains("Starter description for your Webstir app.", StringComparison.Ordinal));
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public void HtmlDevelopmentIncludesRuntimeScripts()
    {
        if (!WorkspaceManager.EnsureLocalPackagesReady())
        {
            throw new ConditionalSkipException("Skipping HTML pipeline tests: framework packages not available (set NPM_TOKEN).");
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.Default(_fixture.Context);
        HtmlPageResult homePage = scenario.GetPage(Folders.Home);

        Assert.False(string.IsNullOrWhiteSpace(homePage.Manifest.Js), "Page manifest should include JS entry in development mode.");
    }

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Full)]
    public void HtmlPublishIncludesModuleScriptInOutput()
    {
        if (!TestCategoryGuards.ShouldRun(TestCategory.Full))
        {
            return;
        }

        HtmlPublishScenarioResult scenario = HtmlPublishScenarios.HeadCombined(_fixture.Context);
        string distHtml = scenario.GetPage(Folders.Home).Html;

        Assert.True(distHtml.Contains("<script type=\"module\" src=\"/pages/home/index", StringComparison.Ordinal));
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while (true)
        {
            index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }
            count++;
            index += value.Length;
        }
        return count;
    }
}
