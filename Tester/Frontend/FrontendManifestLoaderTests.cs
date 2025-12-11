using System;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using Engine;
using Engine.Bridge.Frontend;
using Tester.Infrastructure;
using Xunit;

namespace Tester.Frontend;

public sealed class FrontendManifestLoaderTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    [Fact]
    [Trait(TestTraits.Category, TestTraits.Quick)]
    public async Task ReadsWorkspaceManifestAsync()
    {
        TestCaseContext context = new(new Runner(), Paths.OutPath);
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

        FrontendManifest manifest = await FrontendManifestLoader.LoadAsync(workspace);

        Assert.Equal(1, manifest.Version);
        Assert.Equal(workspaceRoot, manifest.Paths.Workspace);
        Assert.Equal(Path.Combine(workspaceRoot, "dist", "frontend"), manifest.Paths.Dist.Frontend);
        Assert.True(manifest.Features.HtmlSecurity);
        Assert.True(manifest.Features.ImageOptimization);
        Assert.True(manifest.Features.Precompression);
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
            Content = Path.Combine(basePath, Folders.Frontend, Folders.Pages, "docs"),
            Images = Path.Combine(basePath, Folders.Frontend, Folders.Images),
            Fonts = Path.Combine(basePath, Folders.Frontend, Folders.Fonts),
            Media = Path.Combine(basePath, Folders.Frontend, Folders.Media)
        };
    }
}
