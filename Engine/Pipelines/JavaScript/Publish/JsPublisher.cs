using System;
using System.IO;
using System.Threading.Tasks;

namespace Engine.Pipelines.JavaScript.Publish;

public class JsPublisher(AppWorkspace workspace)
{
    public async Task PublishAsync()
    {
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
                content = JsRegex.SourceMapLine().Replace(content, string.Empty);
                content = JsRegex.SourceMapBlock().Replace(content, string.Empty);
                await File.WriteAllTextAsync(destination, content);
            }
            else
            {
                File.Copy(sourceFile, destination, true);
            }
        }
    }
}
