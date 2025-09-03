using Engine.Helpers;
using Engine.Models;
using Engine.Pipelines.Assets;
using Engine.Pipelines.Css;
using Engine.Pipelines.Html;
using Engine.Pipelines.JavaScript;

namespace Engine.Workers;

public partial class ClientWorker(
    AppWorkspace workspace,
    HtmlHandler htmlHandler,
    CssHandler cssHandler,
    JsHandler scriptsHandler,
    AssetHandler imagesHandler) : IWorker
{
    public int BuildOrder => 1;

    public async Task InitAsync(ProjectMode mode) =>
        await ResourceHelpers.CopyEmbeddedDirectoryAsync(Templates.ClientPath, workspace.ClientPath);

    public async Task BuildAsync(string? changedFilePath = null)
    {
        if (!string.IsNullOrEmpty(changedFilePath) && !BuildHelpers.ContainsBuildFolder(changedFilePath, Folders.Client))
        {
            return;
        }

        await scriptsHandler.BuildAsync();
        await Task.WhenAll(
            htmlHandler.BuildAsync(),
            cssHandler.BuildAsync(),
            imagesHandler.BuildAsync()
        );
    }

    public async Task PublishAsync()
    {
        if (Directory.Exists(workspace.ClientDistPath))
        {
            TryClearDirectory(workspace.ClientDistPath);
        }
        else
        {
            Directory.CreateDirectory(workspace.ClientDistPath);
        }
        
        await Task.WhenAll(
            cssHandler.PublishAsync(),
            scriptsHandler.PublishAsync()
        );

        await htmlHandler.PublishAsync();
        await imagesHandler.PublishAsync();

        await PublishAppAssetsAsync();
    }

    public async Task AddPageAsync(string pageName)
    {
        await Task.WhenAll(
            htmlHandler.AddPageAsync(pageName),
            cssHandler.AddPageAsync(pageName),
            scriptsHandler.AddPageAsync(pageName),
            AssetHandler.AddPageAsync(pageName)
        );
    }
    private async Task PublishAppAssetsAsync()
    {
        // Copy client app assets from build to dist, excluding source maps,
        // and strip any sourceMappingURL comments from JS files.
        string sourceApp = workspace.ClientBuildAppPath;
        string destApp = workspace.ClientDistAppPath;

        if (!Directory.Exists(sourceApp))
        {
            return;
        }

        Directory.CreateDirectory(destApp);

        foreach (string sourceFile in Directory.GetFiles(sourceApp, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceApp, sourceFile);
            string destination = Path.Combine(destApp, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            if (sourceFile.EndsWith(FileExtensions.Map, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sourceFile.EndsWith(FileExtensions.Js, StringComparison.OrdinalIgnoreCase))
            {
                string content = await File.ReadAllTextAsync(sourceFile);
                // Remove sourceMappingURL comments if present (both line and block forms)
                content = SourceMapLineRegex().Replace(content, string.Empty);
                content = SourceMapBlockRegex().Replace(content, string.Empty);
                await File.WriteAllTextAsync(destination, content);
            }
            else
            {
                File.Copy(sourceFile, destination, true);
            }
        }
    }

    private static void TryClearDirectory(string path)
    {
        try
        {
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); } catch { }
            }
            foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
        catch { }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*\/\/\#\s*sourceMappingURL=.*$", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex SourceMapLineRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"\/\*\#\s*sourceMappingURL=.*?\*\/\s*$", System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex SourceMapBlockRegex();
}
