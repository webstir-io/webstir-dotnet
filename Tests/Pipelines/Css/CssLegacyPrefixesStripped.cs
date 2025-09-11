using System;
using System.IO;
using Engine;

using Tests.Framework;

namespace Tests.Pipelines.Css;

public sealed class CssLegacyPrefixesStripped : ITestCase
{
    public string Name => "Legacy CSS prefixes and values are stripped in publish";
    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string testDirectory = Paths.OutPath;
        Directory.CreateDirectory(testDirectory);
        string seedDirectory = Path.Combine(testDirectory, Folders.Seed);

        if (!Directory.Exists(Path.Combine(seedDirectory, Folders.Src)))
        {
            ProcessRunner.ProcessResult init = context.Cli.Run(Commands.Init, testDirectory, timeoutMs: 10000);
            Assert.AreEqual(0, init.ExitCode, $"{Commands.Init} command failed. Error: {init.Error}");
        }

        string pageCss = Path.Combine(seedDirectory, Folders.Src, Folders.Frontend, Folders.Pages, Folders.Home, $"{Files.Index}{FileExtensions.Css}");
        string rules = 
            ".ms{ -ms-user-select:none; -ms-transform:rotate(10deg); }" +
            ".o{ -o-user-select:none; }" +
            ".k{ -khtml-user-select:none; }" +
            ".legacyflex{ display:-webkit-box; display:-ms-flexbox; display:flex }" +
            ".ok{ -webkit-user-select:none; -webkit-appearance:none; -webkit-text-size-adjust:100%; -webkit-overflow-scrolling:touch; -webkit-line-clamp:2; -webkit-background-clip:text; -webkit-mask-image:none; }\n";
        File.AppendAllText(pageCss, rules);

        string seedBuild = Path.Combine(seedDirectory, Folders.Build);
        string seedDist = Path.Combine(seedDirectory, Folders.Dist);
        if (Directory.Exists(seedBuild)) { try { Directory.Delete(seedBuild, recursive: true); } catch { } }
        if (Directory.Exists(seedDist)) { try { Directory.Delete(seedDist, recursive: true); } catch { } }

        ProcessRunner.ProcessResult result = context.Cli.Run($"{Commands.Publish} {ProjectOptions.ProjectName} seed", testDirectory, timeoutMs: 20000);
        Assert.AreEqual(0, result.ExitCode, $"{Commands.Publish} command failed. Error: {result.Error}");
        context.AssertNoCompilationErrors(result);

        string clientPageDirectory = Path.Combine(seedDirectory, Folders.Dist, Folders.Frontend, Folders.Pages, Folders.Home);
        Engine.Pipelines.Core.AssetManifest manifest = Engine.Pipelines.Core.AssetManifest.Load(clientPageDirectory);
        string expectedCssPath = !string.IsNullOrWhiteSpace(manifest.Css)
            ? Path.Combine(clientPageDirectory, manifest.Css!)
            : Path.Combine(clientPageDirectory, $"{Files.Index}{FileExtensions.Css}");
        Assert.IsTrue(File.Exists(expectedCssPath), "CSS file missing in dist (checked via manifest)");

        string css = File.ReadAllText(expectedCssPath);
        // Stripped
        Assert.DoesNotContain("-ms-", css, "All -ms- prefixed declarations should be removed");
        Assert.DoesNotContain("-o-", css, "All -o- prefixed declarations should be removed");
        Assert.DoesNotContain("-khtml-", css, "All -khtml- prefixed declarations should be removed");
        Assert.DoesNotContain("display:-webkit-box", css, "Legacy -webkit-box should be removed");
        Assert.DoesNotContain("display:-ms-flexbox", css, "Legacy -ms-flexbox should be removed");

        // Kept allowlisted webkit props
        Assert.Contains("-webkit-user-select", css, "Allowed -webkit-user-select should remain");
        Assert.Contains("-webkit-appearance", css, "Allowed -webkit-appearance should remain");
        Assert.Contains("-webkit-text-size-adjust", css, "Allowed -webkit-text-size-adjust should remain");
        Assert.Contains("-webkit-overflow-scrolling", css, "Allowed -webkit-overflow-scrolling should remain");
        Assert.Contains("-webkit-line-clamp", css, "Allowed -webkit-line-clamp should remain");
        Assert.Contains("-webkit-background-clip", css, "Allowed -webkit-background-clip should remain");
        Assert.Contains("-webkit-mask-image", css, "Allowed -webkit-mask-* should remain");
    }
}

