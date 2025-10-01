using System;
using System.IO;
using System.Text.Json;
using Engine;
using Engine.Bridge.Frontend;
using Tests.Framework;

namespace Tests.Frontend;

public sealed class FrontendManifestLoaderReadsWorkspaceManifest : ITestCase
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public string Name => "Frontend manifest loader reads workspace manifest";

    public TestCategory Category => TestCategory.Quick;

    public void Execute(TestCaseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string workspaceRoot = Path.Combine(context.OutPath, "frontend-manifest-loader");
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }

        Directory.CreateDirectory(workspaceRoot);
        string toolsDirectory = Path.Combine(workspaceRoot, ".webstir");
        Directory.CreateDirectory(toolsDirectory);

        string manifestPath = Path.Combine(toolsDirectory, Files.FrontendManifestJson);
        WriteManifest(manifestPath, workspaceRoot);

        AppWorkspace workspace = new();
        workspace.Initialize(workspaceRoot);

        FrontendManifest manifest = FrontendManifestLoader.LoadAsync(workspace).GetAwaiter().GetResult();

        Assert.AreEqual(1, manifest.Version, "Manifest version should be 1");
        Assert.AreEqual(workspaceRoot, manifest.Paths.Workspace, "Workspace path mismatch");
        Assert.AreEqual(Path.Combine(workspaceRoot, "dist", "frontend"), manifest.Paths.Dist.Frontend, "Dist frontend path mismatch");
        Assert.IsTrue(manifest.Features.HtmlSecurity, "HtmlSecurity flag should be true");
        Assert.IsTrue(manifest.Features.ImageOptimization, "ImageOptimization flag should be true");
        Assert.IsTrue(manifest.Features.Precompression, "Precompression flag should be true");
    }

    private static void WriteManifest(string manifestPath, string workspaceRoot)
    {
        FrontendManifest manifest = new()
        {
            Version = 1,
            Paths = new FrontendManifestPaths
            {
                Workspace = workspaceRoot,
                Src = BuildGroup(workspaceRoot, Folders.Src),
                Build = BuildGroup(workspaceRoot, Folders.Build),
                Dist = BuildGroup(workspaceRoot, Folders.Dist)
            },
            Features = new FrontendManifestFeatures
            {
                HtmlSecurity = true,
                ImageOptimization = true,
                Precompression = true
            }
        };

        string json = JsonSerializer.Serialize(manifest, SerializerOptions);
        File.WriteAllText(manifestPath, json);
    }

    private static FrontendManifestPathGroup BuildGroup(string workspaceRoot, string folder)
    {
        string basePath = Path.Combine(workspaceRoot, folder);
        return new FrontendManifestPathGroup
        {
            Root = basePath,
            Frontend = Path.Combine(basePath, Folders.Frontend),
            App = Path.Combine(basePath, Folders.Frontend, Folders.App),
            Pages = Path.Combine(basePath, Folders.Frontend, Folders.Pages),
            Images = Path.Combine(basePath, Folders.Frontend, Folders.Images),
            Fonts = Path.Combine(basePath, Folders.Frontend, Folders.Fonts),
            Media = Path.Combine(basePath, Folders.Frontend, Folders.Media)
        };
    }
}
