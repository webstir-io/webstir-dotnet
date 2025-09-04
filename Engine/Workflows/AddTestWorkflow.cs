using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Engine.Extensions;
using Engine.Workers;

namespace Engine.Workflows;

public sealed class AddTestWorkflow(AppWorkspace context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker) : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    public override string WorkflowName => Commands.AddTest;

    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        string[] filtered = [.. args.Where(a => a != WorkflowName)];
        string? nameOrPath = filtered.FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(nameOrPath))
        {
            Console.WriteLine("Please specify a test name or path. See 'webstir help add-test'.");
            return;
        }

        string rel = nameOrPath!.Trim().Trim('/', '\\');
        bool hasSlash = rel.Contains('/') || rel.Contains('\\');
        string targetDir;
        string fileName;

        if (hasSlash)
        {
            // Treat as relative to src
            int extLen = ($"{Files.Test}{FileExtensions.Ts}").Length;
            string withoutExt = rel.EndsWith($"{Files.Test}{FileExtensions.Ts}", StringComparison.OrdinalIgnoreCase)
                ? rel[..^extLen]
                : rel;

            string parent = Path.GetDirectoryName(withoutExt) ?? string.Empty;
            string leaf = Path.GetFileName(withoutExt);
            targetDir = Context.SrcPath.Combine(parent, Folders.Tests);
            fileName = leaf + Files.Test + FileExtensions.Ts;
        }
        else
        {
            targetDir = Context.SrcPath.Combine(Folders.Tests);
            fileName = rel + Files.Test + FileExtensions.Ts;
        }

        Directory.CreateDirectory(targetDir);
        string targetFile = targetDir.Combine(fileName);

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
        string typesDir = Context.WorkingPath.Combine("types");
        Directory.CreateDirectory(typesDir);
        string typesFile = typesDir.Combine("w-test.d.ts");
        if (!File.Exists(typesFile))
        {
            await File.WriteAllTextAsync(typesFile, TypesDtsContent);
            Console.WriteLine($"Added types: {Path.GetRelativePath(Context.WorkingPath, typesFile)}");
        }

        string tsconfig = Context.WorkingPath.Combine("base.tsconfig.json");
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
                        JsonArray roots = ["./types", "./node_modules/@types"];
                        compilerOptions["typeRoots"] = roots;
                        await File.WriteAllTextAsync(tsconfig, obj.ToJsonString(new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = true
                        }));
                        Console.WriteLine("Updated base.tsconfig.json with typeRoots");
                    }
                }
            }
            catch
            {
                // Ignore malformed tsconfig, do not fail the command.
            }
        }
    }

    private const string SampleTestContent = """
test('sample passes', () => {
  assert.isTrue(true);
});
""";

    private const string TypesDtsContent = """
declare function test(name: string, fn?: () => unknown | Promise<unknown>): void;
declare namespace assert {
  function isTrue(value: unknown, message?: string): void;
  function equal<T>(expected: T, actual: T, message?: string): void;
  function fail(message: string): never;
}
""";
}
