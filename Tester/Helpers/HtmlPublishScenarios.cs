using System;
using System.Collections.Generic;
using System.IO;
using Engine;
using Utilities.Process;
using Tester.Infrastructure;
using Xunit;
using Assert = Tester.Helpers.LegacyAssert;
using Tester.Helpers;

namespace Tester.Helpers;

internal static class HtmlPublishScenarios
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, HtmlPublishScenarioResult> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, object> KeyLocks = new(StringComparer.OrdinalIgnoreCase);

    private static object GetKeyLock(string key)
    {
        lock (Sync)
        {
            if (!KeyLocks.TryGetValue(key, out object? lockObj))
            {
                lockObj = new object();
                KeyLocks[key] = lockObj;
            }
            return lockObj;
        }
    }

    public static HtmlPublishScenarioResult Default(TestCaseContext context) =>
        GetOrCreate(context, "default", workspace =>
        {
            // Speed defaults: keep htmlSecurity on; disable imageOptimization and precompression
            string frontendRoot = Path.Combine(workspace.WorkspacePath, Folders.Src, Folders.Frontend);
            Directory.CreateDirectory(frontendRoot);
            string configPath = Path.Combine(frontendRoot, "frontend.config.json");
            string config = """
{
  "htmlSecurity": true,
  "imageOptimization": false,
  "precompression": false
}
""";
            File.WriteAllText(configPath, config);

            workspace.CleanOutputs();
            ProcessResult publish = workspace.Publish();
            Assert.IsFalse(publish.TimedOut, "Publish timed out for html-default.");
            return workspace.CreateResult(publish, Folders.Home);
        });

    public static HtmlPublishScenarioResult AttributesAndComments(TestCaseContext context) =>
        GetOrCreate(context, "attributes-comments", workspace =>
        {
            string pagePath = Path.Combine(
                workspace.WorkspacePath,
                Folders.Src,
                Folders.Frontend,
                Folders.Pages,
                Folders.Home,
                $"{Files.Index}{FileExtensions.Html}");

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

            workspace.CleanOutputs();
            ProcessResult publish = workspace.Publish();
            Assert.IsFalse(publish.TimedOut, "Publish timed out for html-attributes-comments.");
            return workspace.CreateResult(publish, Folders.Home);
        });

    public static HtmlPublishScenarioResult MetaOverrides(TestCaseContext context) =>
        GetOrCreate(context, "meta-overrides", workspace =>
        {
            string pageDir = Path.Combine(
                workspace.WorkspacePath,
                Folders.Src,
                Folders.Frontend,
                Folders.Pages,
                Folders.Home);

            Directory.CreateDirectory(pageDir);
            string pageHtmlPath = Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Html}");
            string pageHtml = """
<head>
    <title>Home</title>
    <meta name="viewport" content="page-viewport">
    <meta name="description" content="page-desc">
    <meta property="og:title" content="OG Page Title">
    <link rel="canonical" href="/home" />
    <script data-test="head-script">console.log('in-head');</script>
    <link rel="stylesheet" href="index.css" />
    <script type="module" src="index.js" async></script>
</head>
<body>
    <main>
        Home
    </main>
</body>
""";
            File.WriteAllText(pageHtmlPath, pageHtml);

            workspace.CleanOutputs();
            ProcessResult publish = workspace.Publish();
            Assert.IsFalse(publish.TimedOut, "Publish timed out for html-meta-overrides.");
            return workspace.CreateResult(publish, Folders.Home);
        });

    public static HtmlPublishScenarioResult HeadOrdering(TestCaseContext context) =>
        GetOrCreate(context, "head-ordering", workspace =>
        {
            string appHtmlPath = Path.Combine(
                workspace.WorkspacePath,
                Folders.Src,
                Folders.Frontend,
                Folders.App,
                "app.html");
            string appHtml = File.ReadAllText(appHtmlPath);
            appHtml = appHtml.Replace(
                "</head>",
                "    <link rel=\"alternate\" hreflang=\"en\" href=\"/en/home\" />\n" +
                "    <link rel=\"alternate\" hreflang=\"fr\" href=\"/fr/home\" />\n" +
                "</head>");
            File.WriteAllText(appHtmlPath, appHtml);

            string pageDir = Path.Combine(
                workspace.WorkspacePath,
                Folders.Src,
                Folders.Frontend,
                Folders.Pages,
                Folders.Home);
            Directory.CreateDirectory(pageDir);
            string pageHtmlPath = Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Html}");
            string pageHtml = """
<head>
    <title>Home</title>
    <meta name="viewport" content="page-viewport">
    <link rel="alternate" hreflang="en" href="/en/home-page" />
    <link rel="stylesheet" href="index.css" />
    <script type="module" src="index.js" async></script>
</head>
<body>
    <main>Home</main>
</body>
""";
            File.WriteAllText(pageHtmlPath, pageHtml);

            workspace.CleanOutputs();
            ProcessResult publish = workspace.Publish();
            Assert.IsFalse(publish.TimedOut, "Publish timed out for html-head-ordering.");
            return workspace.CreateResult(publish, Folders.Home);
        });

    public static HtmlPublishScenarioResult HeadCombined(TestCaseContext context) =>
        GetOrCreate(context, "head-combined", workspace =>
        {
            // Speed defaults for combined scenario
            string frontendRootForConfig = Path.Combine(workspace.WorkspacePath, Folders.Src, Folders.Frontend);
            Directory.CreateDirectory(frontendRootForConfig);
            string cfgPath = Path.Combine(frontendRootForConfig, "frontend.config.json");
            string cfg = """
{
  "htmlSecurity": true,
  "imageOptimization": false,
  "precompression": false
}
""";
            File.WriteAllText(cfgPath, cfg);

            // Add alternates to app template (template-level)
            string appHtmlPath = Path.Combine(
                workspace.WorkspacePath,
                Folders.Src,
                Folders.Frontend,
                Folders.App,
                "app.html");
            string appHtml = File.ReadAllText(appHtmlPath);
            appHtml = appHtml.Replace(
                "</head>",
                "    <link rel=\"alternate\" hreflang=\"en\" href=\"/en/home\" />\n" +
                "    <link rel=\"alternate\" hreflang=\"fr\" href=\"/fr/home\" />\n" +
                "</head>");
            File.WriteAllText(appHtmlPath, appHtml);

            // Page overrides + attributes/comments markup
            string pageDir = Path.Combine(
                workspace.WorkspacePath,
                Folders.Src,
                Folders.Frontend,
                Folders.Pages,
                Folders.Home);
            Directory.CreateDirectory(pageDir);
            string pageHtmlPath = Path.Combine(pageDir, $"{Files.Index}{FileExtensions.Html}");
            string pageHtml = """
<head>
    <!-- head comment should be removed -->
    <title>Home</title>
    <meta name="viewport" content="page-viewport">
    <meta name="description" content="page-desc">
    <meta property="og:title" content="OG Page Title">
    <link rel="canonical" href="/home" />
    <script data-test="head-script">console.log('in-head');</script>
    <link rel="stylesheet" href="index.css" />
    <script type="module" src="index.js" async></script>
</head>
<body>
    <main>
        <!-- body comment should be removed -->
        <button disabled="disabled" class="primary" data-info="foo bar">Click</button>
        <a class="link" rel="nofollow">Link</a>
        Home
    </main>
</body>
""";
            File.WriteAllText(pageHtmlPath, pageHtml);

            workspace.CleanOutputs();
            ProcessResult publish = workspace.Publish();
            Assert.IsFalse(publish.TimedOut, "Publish timed out for html-head-combined.");
            return workspace.CreateResult(publish, Folders.Home);
        });

    public static HtmlPublishScenarioResult PerfPage(TestCaseContext context) =>
        GetOrCreate(context, "perf", workspace =>
        {
            string pageRoot = Path.Combine(
                workspace.WorkspacePath,
                Folders.Src,
                Folders.Frontend,
                Folders.Pages,
                "perf");
            Directory.CreateDirectory(pageRoot);

            string cssPath = Path.Combine(pageRoot, $"{Files.Index}{FileExtensions.Css}");
            File.WriteAllText(cssPath, "body{color:#123}" + Environment.NewLine);

            string imagesRoot = Path.Combine(
                workspace.WorkspacePath,
                Folders.Src,
                Folders.Frontend,
                Folders.Images);
            Directory.CreateDirectory(imagesRoot);
            string pngPath = Path.Combine(imagesRoot, "test.png");
            byte[] png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8Xw8AApMBgTF+tYcAAAAASUVORK5CYII=");
            File.WriteAllBytes(pngPath, png);

            string htmlPath = Path.Combine(pageRoot, $"{Files.Index}{FileExtensions.Html}");
            string html = """
<head>
    <meta charset="utf-8">
    <title>perf</title>
    <link rel="stylesheet" href="index.css">
    <script type="module" src="index.js" async></script>
</head>
<body>
    <main>
        <h1>perf</h1>
        <p>Content for the perf page.</p>

        <img src="/images/test.png" alt="a">
        <img src="/images/test.png" alt="b">
        <a href="/home">home</a>
    </main>
</body>
""";
            File.WriteAllText(htmlPath, html);

            string tsPath = Path.Combine(pageRoot, $"{Files.Index}{FileExtensions.Ts}");
            File.WriteAllText(tsPath, "import '../../app/app';\n");

            workspace.CleanOutputs();
            ProcessResult publish = workspace.Publish();
            Assert.IsFalse(publish.TimedOut, "Publish timed out for html-perf.");
            return workspace.CreateResult(publish, "perf");
        });

    public static HtmlPublishScenarioResult FeatureFlagsDisabled(TestCaseContext context) =>
        GetOrCreate(context, "feature-flags-disabled", workspace =>
        {
            string frontendRoot = Path.Combine(workspace.WorkspacePath, Folders.Src, Folders.Frontend);
            Directory.CreateDirectory(frontendRoot);

            string pagesHomeDir = Path.Combine(frontendRoot, Folders.Pages, Folders.Home);
            Directory.CreateDirectory(pagesHomeDir);

            string imagesRoot = Path.Combine(frontendRoot, Folders.Images);
            Directory.CreateDirectory(imagesRoot);
            string pngPath = Path.Combine(imagesRoot, "test.png");
            byte[] png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8Xw8AApMBgTF+tYcAAAAASUVORK5CYII=");
            File.WriteAllBytes(pngPath, png);

            string cssPath = Path.Combine(pagesHomeDir, $"{Files.Index}{FileExtensions.Css}");
            File.WriteAllText(cssPath, "main{padding:4px}\n");

            string htmlPath = Path.Combine(pagesHomeDir, $"{Files.Index}{FileExtensions.Html}");
            string html = """
<head>
    <title>Feature Flags</title>
    <link rel="stylesheet" href="index.css" />
    <script type="module" src="index.js"></script>
</head>
<body>
    <main>
        <img src="/images/test.png" alt="primary" />
        <img src="/images/test.png" alt="secondary" />
    </main>
</body>
""";
            File.WriteAllText(htmlPath, html);

            string configPath = Path.Combine(frontendRoot, "frontend.config.json");
            string config = """
{
  "htmlSecurity": false,
  "imageOptimization": false,
  "precompression": false
}
""";
            File.WriteAllText(configPath, config);

            workspace.CleanOutputs();
            ProcessResult publish = workspace.Publish();
            Assert.IsFalse(publish.TimedOut, "Publish timed out for html-feature-flags-disabled.");
            return workspace.CreateResult(publish, Folders.Home);
        });

    public static HtmlPublishScenarioResult PrecompressionEnabled(TestCaseContext context) =>
        GetOrCreate(context, "precompression-on", workspace =>
        {
            string frontendRoot = Path.Combine(workspace.WorkspacePath, Folders.Src, Folders.Frontend);
            Directory.CreateDirectory(frontendRoot);

            // Turn on precompression only to keep scenario light
            string configPath = Path.Combine(frontendRoot, "frontend.config.json");
            string config = """
{
  "precompression": true,
  "imageOptimization": false,
  "htmlSecurity": true
}
""";
            File.WriteAllText(configPath, config);

            workspace.CleanOutputs();
            ProcessResult publish = workspace.Publish();
            Assert.IsFalse(publish.TimedOut, "Publish timed out for html-precompression-on.");
            return workspace.CreateResult(publish, Folders.Home);
        });

    private static HtmlPublishScenarioResult GetOrCreate(
        TestCaseContext context,
        string key,
        Func<HtmlPublishScenarioWorkspace, HtmlPublishScenarioResult> factory)
    {
        // Fast path with on-disk validation to avoid stale cached results
        lock (Sync)
        {
            if (Cache.TryGetValue(key, out HtmlPublishScenarioResult? cached) && cached is not null)
            {
                if (System.IO.Directory.Exists(cached.DistFrontendPath))
                {
                    return cached;
                }
                // Fall through to rebuild if dist output has been cleaned
            }
        }

        // Per-key lock so different scenarios can build concurrently
        object keyLock = GetKeyLock(key);
        lock (keyLock)
        {
            // Re-check after acquiring the lock; validate output exists
            if (Cache.TryGetValue(key, out HtmlPublishScenarioResult? cached) && cached is not null)
            {
                if (System.IO.Directory.Exists(cached.DistFrontendPath))
                {
                    return cached;
                }
                // cached is stale; rebuild below
            }

            HtmlPublishScenarioWorkspace workspace = HtmlPublishScenarioWorkspace.Create(context, key);
            HtmlPublishScenarioResult result = factory(workspace);
            lock (Sync)
            {
                Cache[key] = result;
            }
            return result;
        }
    }
}

internal sealed class HtmlPublishScenarioWorkspace
{
    private readonly TestCaseContext _context;

    private HtmlPublishScenarioWorkspace(TestCaseContext context, string workspaceName, string workspacePath)
    {
        _context = context;
        WorkspaceName = workspaceName;
        WorkspacePath = workspacePath;
    }

    public string WorkspaceName
    {
        get;
    }

    public string WorkspacePath
    {
        get;
    }

    public string BuildPath => Path.Combine(WorkspacePath, Folders.Build);
    public string DistPath => Path.Combine(WorkspacePath, Folders.Dist);
    public string DistFrontendPath => Path.Combine(DistPath, Folders.Frontend);

    public static HtmlPublishScenarioWorkspace Create(TestCaseContext context, string scenarioKey)
    {
        string workspaceName = $"html-{scenarioKey}";
        string workspacePath = WorkspaceManager.CreateSeedWorkspace(context, workspaceName);
        return new HtmlPublishScenarioWorkspace(context, workspaceName, workspacePath);
    }

    public void CleanOutputs()
    {
        TryDeleteDirectory(BuildPath);
        TryDeleteDirectory(DistPath);
    }

    public ProcessResult Publish(int timeoutMs = 45000)
    {
        return _context.Run(
            $"{Commands.Publish} {ProjectOptions.ProjectName} {WorkspaceName}",
            _context.OutPath,
            timeoutMs: timeoutMs);
    }

    public ProcessResult RunCommand(string arguments, int timeoutMs) =>
        _context.Run(arguments, _context.OutPath, timeoutMs: timeoutMs);

    public HtmlPublishScenarioResult CreateResult(
        ProcessResult publishResult,
        params string[] pagesToPrime)
    {
        HtmlPublishScenarioResult result = new HtmlPublishScenarioResult(
            WorkspacePath,
            DistFrontendPath,
            publishResult);

        foreach (string page in pagesToPrime)
        {
            result.GetPage(page);
        }

        return result;
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }
}

internal sealed class HtmlPublishScenarioResult
{
    private readonly Dictionary<string, HtmlPageResult> _pages = new(StringComparer.OrdinalIgnoreCase);

    public HtmlPublishScenarioResult(
        string workspacePath,
        string distFrontendPath,
        ProcessResult publishResult)
    {
        WorkspacePath = workspacePath;
        DistFrontendPath = distFrontendPath;
        PublishResult = publishResult;
    }

    public string WorkspacePath
    {
        get;
    }

    public string DistFrontendPath
    {
        get;
    }

    public ProcessResult PublishResult
    {
        get;
    }

    public HtmlPageResult GetPage(string pageName)
    {
        if (_pages.TryGetValue(pageName, out HtmlPageResult? page) && page is not null)
        {
            return page;
        }

        string pageDirectory = Path.Combine(DistFrontendPath, Folders.Pages, pageName);
        HtmlPageResult resolved = HtmlPageResult.Create(pageName, pageDirectory);
        _pages[pageName] = resolved;
        return resolved;
    }
}

internal sealed class HtmlPageResult
{
    private HtmlPageResult(
        string pageName,
        string directory,
        string htmlPath,
        string html,
        PageAssetManifest manifest)
    {
        PageName = pageName;
        DirectoryPath = directory;
        HtmlPath = htmlPath;
        Html = html;
        Manifest = manifest;
    }

    public string PageName
    {
        get;
    }

    public string DirectoryPath
    {
        get;
    }

    public string HtmlPath
    {
        get;
    }

    public string Html
    {
        get;
    }

    public string HtmlNormalized => Html.Replace("\r", string.Empty, StringComparison.Ordinal);

    public PageAssetManifest Manifest
    {
        get;
    }

    public static HtmlPageResult Create(string pageName, string directory)
    {
        Assert.IsTrue(System.IO.Directory.Exists(directory), $"Dist directory missing for page '{pageName}'");

        string htmlPath = Path.Combine(directory, $"{Files.Index}{FileExtensions.Html}");
        Assert.IsTrue(File.Exists(htmlPath), $"Dist HTML missing for page '{pageName}'");

        string html = File.ReadAllText(htmlPath);
        PageAssetManifest manifest = PageAssetManifest.Load(directory);

        return new HtmlPageResult(pageName, directory, htmlPath, html, manifest);
    }
}
