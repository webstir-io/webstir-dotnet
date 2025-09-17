using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Engine.Extensions;
using System.Collections.Generic;
using Engine.Helpers;
using Engine.Interfaces;

namespace Engine.Workflows;

public sealed class AddTestWorkflow(AppWorkspace context,
    IEnumerable<IWorkflowWorker> workers) : BaseWorkflow(context, workers)
{
    public override string WorkflowName => Commands.AddTest;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string[] filtered = [.. args.Where(arg => arg != WorkflowName)];
        string? nameOrPath = filtered.FirstOrDefault(arg => !arg.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(nameOrPath))
        {
            Console.WriteLine("Please specify a test name or path. See 'webstir help add-test'.");
            return;
        }

        string relativePath = nameOrPath!.Trim().Trim('/', '\\');
        bool hasSlash = relativePath.Contains('/') || relativePath.Contains('\\');
        string targetDirectory;
        string fileName;

        if (hasSlash)
        {
            // Treat as relative to src
            int extensionLength = (Files.Test + FileExtensions.Ts).Length;
            string withoutExtension = relativePath.EndsWith(Files.Test + FileExtensions.Ts, StringComparison.OrdinalIgnoreCase)
                ? relativePath[..^extensionLength]
                : relativePath;

            string parent = Path.GetDirectoryName(withoutExtension) ?? string.Empty;
            string leaf = Path.GetFileName(withoutExtension);
            targetDirectory = Context.SrcPath.Combine(parent, Folders.Tests);
            fileName = leaf + Files.Test + FileExtensions.Ts;
        }
        else
        {
            targetDirectory = Context.SrcPath.Combine(Folders.Tests);
            fileName = relativePath + Files.Test + FileExtensions.Ts;
        }

        Directory.CreateDirectory(targetDirectory);
        string targetFile = targetDirectory.Combine(fileName);

        if (!File.Exists(targetFile))
        {
            await File.WriteAllTextAsync(targetFile, SampleTestContent);
            Console.WriteLine($"Created {Path.GetRelativePath(Context.WorkingPath, targetFile)}");
        }
        else
        {
            Console.WriteLine($"File already exists: {Path.GetRelativePath(Context.WorkingPath, targetFile)}");
        }

        await EnsurePackageAsync();
    }

    private async Task EnsurePackageAsync()
    {
        PackageEnsureResult result = await TestPackageInstaller.EnsureAsync(Context);

        if (result.ToolsAdded)
        {
            Console.WriteLine($"Added testing package archive: {Path.Combine(Folders.Tools, result.Metadata.FileName)}");
        }

        if (result.DependencyUpdated)
        {
            Console.WriteLine($"Pinned @webstir/test to {result.Metadata.Dependency} in {Files.PackageJson}");
        }

        if (result.VersionMismatch)
        {
            string installed = string.IsNullOrWhiteSpace(result.InstalledVersion)
                ? "not installed"
                : $"{result.InstalledVersion}";
            Console.WriteLine($"Warning: @webstir/test {installed} differs from packaged {result.Metadata.Version}. Run 'npm install' to refresh node_modules.");
        }
    }

    private const string SampleTestContent = """
const { test, assert } = require('@webstir/test') as typeof import('@webstir/test');

test('sample passes', () => {
  assert.isTrue(true);
});
""";

}
