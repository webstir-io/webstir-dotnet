using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Engine.Bridge.Test;

internal static class TestHostScript
{
    private const string ScriptFileName = "test-host.mjs";

    public static async Task<string> EnsureAsync(AppWorkspace workspace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        Directory.CreateDirectory(workspace.WebstirPath);
        string targetPath = Path.Combine(workspace.WebstirPath, ScriptFileName);

        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"{Resources.TestHostPath}.{ScriptFileName}";

        await using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream is null)
        {
            throw new InvalidOperationException("Test host script resource not found.");
        }

        await using FileStream fileStream = new(
            targetPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            4096,
            useAsync: true);

        await resourceStream.CopyToAsync(fileStream, cancellationToken);
        return targetPath;
    }
}
