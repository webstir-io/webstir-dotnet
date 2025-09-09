using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Engine;

namespace Engine.Pipelines.JavaScript.Publish;

public partial class JsAssetsPublisher(AppWorkspace workspace)
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

    [GeneratedRegex(@"^\s*\/\/\#\s*sourceMappingURL=.*$", RegexOptions.Multiline)]
    private static partial Regex SourceMapLineRegex();

    [GeneratedRegex(@"\/\*\#\s*sourceMappingURL=.*?\*\/\s*$", RegexOptions.Singleline)]
    private static partial Regex SourceMapBlockRegex();
}

