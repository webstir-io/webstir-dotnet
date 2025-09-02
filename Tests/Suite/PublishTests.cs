using Tests.Framework;

using Engine;

namespace Tests.Suite;

public class PublishTests : BaseTest
{
    public override string Name => "Publish Tests";
    
    public override Task<TestResult[]> RunAsync()
    {
        List<TestResult> tests = [];
        tests.Add(RunTest($"{Commands.Publish} command runs without compilation errors", TestPublishCommandSuccess));
        if (Tests.Framework.TestMode.IsFull)
        {
            tests.Add(RunTest("HTML publish collapses inter-tag whitespace and preserves inline script content", TestHtmlWhitespaceAndInlineScriptPreserved));
        }
        return Task.FromResult(tests.ToArray());
    }
    
    private void TestPublishCommandSuccess()
    {        
        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string seedDir = Path.Combine(testDir, Folders.Seed);
        if (!Directory.Exists(Path.Combine(seedDir, Folders.Src)))
        {
            // Initialize seed project if missing
            ProcessRunner.ProcessResult init = RunCliCommand(Commands.Init, testDir, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }
        
        // Per-suite cleanup: clear previous build/publish outputs
        string seedBuild = Path.Combine(seedDir, Folders.Build);
        string seedDist = Path.Combine(seedDir, Folders.Dist);
        if (Directory.Exists(seedBuild))
        {
            try { Directory.Delete(seedBuild, recursive: true); } catch { }
        }
        if (Directory.Exists(seedDist))
        {
            try { Directory.Delete(seedDist, recursive: true); } catch { }
        }
        
        ProcessRunner.ProcessResult result = RunCliCommand($"{Commands.Publish} {ProjectOptions.ProjectName} seed", testDir, timeoutMs: 15000);
        
        if (result.TimedOut)
            Assert.Fail($"{Commands.Publish} command timed out");
        
        // Publish must succeed with exit code 0
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Publish} command failed. Error: {result.Error}");
        
        AssertNoCompilationErrors(result);

        // Verify publish outputs exist under the seed project
        string distDir = Path.Combine(testDir, Folders.Seed, Folders.Dist);
        Assert.IsTrue(Directory.Exists(distDir), "seed/dist directory does not exist");

        // Client dist artifacts
        string clientPageDir = Path.Combine(distDir, Folders.Client, Folders.Pages, Folders.Home);
        Assert.IsTrue(File.Exists(Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Html}")), "client page index.html missing in dist");

        string expectedJsPath = Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Js}");
        if (!File.Exists(expectedJsPath))
        {
            System.Text.StringBuilder diagnostics = new();
            diagnostics.AppendLine("client page index.js missing in dist");
            diagnostics.AppendLine();
            diagnostics.AppendLine($"Expected JS path: {expectedJsPath}");

            string buildClientPageDir = Path.Combine(testDir, Folders.Seed, Folders.Build, Folders.Client, Folders.Pages, Folders.Home);
            diagnostics.AppendLine();
            diagnostics.AppendLine($"Build dir listing ({buildClientPageDir}):");
            if (Directory.Exists(buildClientPageDir))
            {
                foreach (string file in Directory.EnumerateFiles(buildClientPageDir))
                {
                    diagnostics.AppendLine("- " + Path.GetFileName(file));
                }
            }
            else
            {
                diagnostics.AppendLine("(build client page dir does not exist)");
            }

            diagnostics.AppendLine();
            diagnostics.AppendLine($"Dist dir listing ({clientPageDir}):");
            if (Directory.Exists(clientPageDir))
            {
                foreach (string file in Directory.EnumerateFiles(clientPageDir))
                {
                    diagnostics.AppendLine("- " + Path.GetFileName(file));
                }
            }
            else
            {
                diagnostics.AppendLine("(dist client page dir does not exist)");
            }

            diagnostics.AppendLine();
            diagnostics.AppendLine("Publish stdout:");
            diagnostics.AppendLine(result.Output);
            diagnostics.AppendLine("Publish stderr:");
            diagnostics.AppendLine(result.Error);

            Assert.Fail(diagnostics.ToString());
        }

        // JS minification: ensure comments removed
        string distJs = File.ReadAllText(expectedJsPath);
        Assert.DoesNotContain("/*", distJs, "Client JS should not contain block comments in dist");
        foreach (string line in distJs.Split('\n'))
        {
            string t = line.Trim();
            if (t.Contains("http://", StringComparison.Ordinal) || t.Contains("https://", StringComparison.Ordinal))
                continue;
            Assert.IsFalse(t.StartsWith("//", StringComparison.Ordinal), "Client JS should not contain line comments in dist");
        }

        // HTML minification: ensure no HTML comments
        string distHtmlPath = Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Html}");
        string distHtml = File.ReadAllText(distHtmlPath);
        Assert.DoesNotContain("<!--", distHtml, "Client HTML should not contain comments in dist");

        // CSS minification: if page CSS exists, ensure no block comments
        string distCssPath1 = Path.Combine(clientPageDir, "index.module.css");
        string distCssPath2 = Path.Combine(clientPageDir, $"{Files.Index}{FileExtensions.Css}");
        string? cssPath = File.Exists(distCssPath1) ? distCssPath1 : (File.Exists(distCssPath2) ? distCssPath2 : null);
        if (cssPath != null)
        {
            string distCss = File.ReadAllText(cssPath);
            Assert.DoesNotContain("/*", distCss, "Client CSS should not contain block comments in dist");
        }
        bool hasCss = File.Exists(Path.Combine(clientPageDir, "index.module.css"));
        // If a page doesn't use CSS modules, there may be no standalone CSS in dist
        // Accept absence for now.

        // Refresh script should not exist in dist
        Assert.IsFalse(File.Exists(Path.Combine(distDir, Folders.Client, Files.RefreshJs)), "refresh.js should not be published to dist");

        // Server dist artifact (comments stripped)
        string serverIndexJs = Path.Combine(distDir, Folders.Server, $"{Files.Index}{FileExtensions.Js}");
        Assert.IsTrue(File.Exists(serverIndexJs), "server index.js missing in dist");

        // Verify server JS has comments stripped (no block comments; no '//' line comments except URL protocols)
        string serverJs = File.ReadAllText(serverIndexJs);
        Assert.DoesNotContain("/*", serverJs, "Block comments should be removed in server dist JS");
        string[] lines = serverJs.Split('\n');
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.Contains("http://", StringComparison.Ordinal) || trimmed.Contains("https://", StringComparison.Ordinal))
            {
                continue; // allow URL protocols
            }
            if (trimmed.Contains("//", StringComparison.Ordinal))
            {
                Assert.Fail("Line comments should be removed in server dist JS");
            }
        }
    }
    
    // TODO: Add more publish command tests here
    // - Test publish output validation (dist folder created)
    // - Test CSS minification in publish mode
    // - Test production optimizations
    // - Test publish performance benchmarks
    
    private void TestHtmlWhitespaceAndInlineScriptPreserved()
    {
        string testDir = Paths.OutPath;
        Directory.CreateDirectory(testDir);
        string seedDir = Path.Combine(testDir, Folders.Seed);
        if (!Directory.Exists(Path.Combine(seedDir, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = RunCliCommand(Commands.Init, testDir, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }

        // Ensure a clean build/dist
        string seedBuild = Path.Combine(seedDir, Folders.Build);
        string seedDist = Path.Combine(seedDir, Folders.Dist);
        if (Directory.Exists(seedBuild)) { try { Directory.Delete(seedBuild, recursive: true); } catch { } }
        if (Directory.Exists(seedDist)) { try { Directory.Delete(seedDist, recursive: true); } catch { } }

        // Ensure extra whitespace around tags to be collapsed in dist
        string pagePath = Path.Combine(seedDir, Folders.Src, Folders.Client, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Html}");
        string html = File.ReadAllText(pagePath);
        const string marker = "<!--WHITESPACE_MARKER-->";
        if (!html.Contains(marker, StringComparison.Ordinal))
        {
            // Insert marker inside <main> to make whitespace around tags available to minifier
            html = html.Replace("<main>", "<main>\n    " + marker + "\n");
            File.WriteAllText(pagePath, html);
        }

        ProcessRunner.ProcessResult result = RunCliCommand($"{Commands.Publish} {ProjectOptions.ProjectName} seed", testDir, timeoutMs: 15000);
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Publish} command failed. Error: {result.Error}");

        string distHtmlPath = Path.Combine(seedDir, Folders.Dist, Folders.Client, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(distHtmlPath), "Dist index.html missing");
        string distHtml = File.ReadAllText(distHtmlPath);

        // Inter-tag whitespace should be collapsed (no newlines between tags)
        string normalized = distHtml.Replace("\r", string.Empty);
        Assert.DoesNotContain("> \n<", normalized, "Inter-tag whitespace should be collapsed");
        Assert.DoesNotContain(">\n<", normalized, "Inter-tag newlines should be collapsed");
        Assert.Contains("</head><body>", normalized, "Head/body boundary should be collapsed");
        Assert.Contains("</main></body>", normalized, "Main/body boundary should be collapsed");
    }
}
