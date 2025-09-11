using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Html;

public sealed class HtmlAttributesAndCommentsOptimized : ITestCase
{
    public string Name => "HTML minifier removes comments and optimizes attributes";
    public TestCategory Category => TestCategory.Full;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        Directory.CreateDirectory(testDirectory);
        string seedDirectory = Path.Combine(testDirectory, Folders.Seed);

        // Ensure seed project exists
        if (!Directory.Exists(Path.Combine(seedDirectory, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = context.Cli.Run(Commands.Init, testDirectory, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }

        // Replace the home page fragment with markup exercising minifier rules
        string pagePath = Path.Combine(seedDirectory, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Html}");
        string content = """
<head>
    <!-- head comment should be removed -->
    <title>Test</title>
</head>
<body>
    <main>
        <!-- body comment should be removed -->
        <button disabled="disabled" class="primary" data-info="foo bar">Click</button>
        <a class="link" rel="nofollow">Link</a>
    </main>
</body>
""";
        File.WriteAllText(pagePath, content);

        // Clean any previous outputs
        string seedBuild = Path.Combine(seedDirectory, Folders.Build);
        string seedDist = Path.Combine(seedDirectory, Folders.Dist);
        if (Directory.Exists(seedBuild))
        {
            try
            {
                Directory.Delete(seedBuild, recursive: true);
            }
            catch { }
        }
        if (Directory.Exists(seedDist))
        {
            try
            {
                Directory.Delete(seedDist, recursive: true);
            }
            catch { }
        }

        // Publish
        ProcessRunner.ProcessResult result = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} seed", testDirectory, timeoutMs: 15000);
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Publish} command failed. Error: {result.Error}");

        string distHtmlPath = Path.Combine(seedDirectory, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(distHtmlPath), "Dist index.html missing");
        string distHtml = File.ReadAllText(distHtmlPath).Replace("\r", string.Empty);

        // Comments removed
        Assert.DoesNotContain("<!--", distHtml, "HTML comments should be removed");

        // Boolean attribute collapsed
        Assert.Contains("<button disabled", distHtml, "Boolean attribute should be collapsed to presence only");
        Assert.DoesNotContain("disabled=\"disabled\"", distHtml, "Boolean attribute should not keep explicit value");

        // Safe unquoting applied
        Assert.Contains(" class=primary", distHtml, "Safe unquoted attribute expected for class=primary");
        Assert.DoesNotContain(" class=\"primary\"", distHtml, "Quoted class attribute should be unquoted when safe");

        // Value with space remains quoted
        Assert.Contains(" data-info=\"foo bar\"", distHtml, "Data attribute with spaces should remain quoted");

        // Another safe unquote case
        Assert.Contains(" rel=nofollow", distHtml, "rel should be unquoted when safe");
    }
}

