using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Engine.Extensions;
using Engine.Workers;

namespace Engine.Workflows;

public sealed class AddTestWorkflow(AppWorkspace context,
    FrontendWorker clientWorker,
    BackendWorker serverWorker,
    SharedWorker sharedWorker) : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
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

        await EnsureTypesAsync();
    }

    private async Task EnsureTypesAsync()
    {
        string typesRoot = Context.WorkingPath.Combine(Folders.Types);
        Directory.CreateDirectory(typesRoot);
        string typesPackageDir = typesRoot.Combine(App.Name);
        Directory.CreateDirectory(typesPackageDir);
        string typesFile = typesPackageDir.Combine(Files.Index + FileExtensions.Dts);
        if (!File.Exists(typesFile))
        {
            await File.WriteAllTextAsync(typesFile, TypesDtsContent);
            Console.WriteLine($"Added types: {Path.GetRelativePath(Context.WorkingPath, typesFile)}");
        }

        string tsconfig = Context.WorkingPath.Combine(Files.BaseTsConfigJson);
        if (File.Exists(tsconfig))
        {
            try
            {
                string json = await File.ReadAllTextAsync(tsconfig);
                JsonNode? root = JsonNode.Parse(json);
                if (root is JsonObject obj)
                {
                    if (obj["compilerOptions"] is not JsonObject compilerOptions)
                    {
                        compilerOptions = [];
                        obj["compilerOptions"] = compilerOptions;
                    }
                    if (compilerOptions["typeRoots"] is null)
                    {
                        JsonArray roots = [$"./{Folders.Types}", $"./{Folders.NodeModules}/@types"];
                        compilerOptions["typeRoots"] = roots;
                        await File.WriteAllTextAsync(tsconfig, obj.ToJsonString(new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = true
                        }));
                        Console.WriteLine("Updated base.tsconfig.json with typeRoots");
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore malformed tsconfig, do not fail the command; log for visibility.
                Console.Error.WriteLine($"Warning: Could not update {Files.BaseTsConfigJson}: {ex.Message}");
            }
        }
    }

    private const string SampleTestContent = """
test('sample passes', () => {
  assert.isTrue(true);
});
""";

    private const string TypesDtsContent = """
export {};

declare global {
  function test(name: string, fn?: () => unknown | Promise<unknown>): void;
  namespace assert {
    function isTrue(value: unknown, message?: string): void;
    function equal<T>(expected: T, actual: T, message?: string): void;
    function fail(message: string): never;
  }
}
""";
}
