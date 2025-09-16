using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Engine.Extensions;

namespace Engine.Helpers;

internal static class TestTypeRegistry
{
    private const string ScopeFolder = "@webstir";
    private const string ModuleFolder = "test";
    private const string ModuleSpecifier = "@webstir/test";
    private const string ScopeStub = "export {};\n";

    private const string ModuleDeclaration = """
export {};

declare module '@webstir/test' {
  type TestCallback = () => unknown | Promise<unknown>;
  export function test(name: string, fn?: TestCallback): void;
  export const assert: {
    isTrue(value: unknown, message?: string): void;
    equal<T>(expected: T, actual: T, message?: string): void;
    fail(message: string): never;
  };
}
""";

    internal static async Task<TypeEnsureResult> EnsureAsync(AppWorkspace workspace)
    {
        string typesRoot = workspace.WorkingPath.Combine(Folders.Types);
        Directory.CreateDirectory(typesRoot);

        string scopedDir = typesRoot.Combine(ScopeFolder);
        Directory.CreateDirectory(scopedDir);

        string moduleDir = scopedDir.Combine(ModuleFolder);
        Directory.CreateDirectory(moduleDir);

        string targetFile = moduleDir.Combine(Files.Index + FileExtensions.Dts);
        string scopeStubFile = scopedDir.Combine(Files.Index + FileExtensions.Dts);

        string legacyDir = typesRoot.Combine(App.Name);
        string legacyFile = legacyDir.Combine(Files.Index + FileExtensions.Dts);

        bool added = false;
        bool migrated = false;
        bool updated = false;

        if (!File.Exists(targetFile) && File.Exists(legacyFile))
        {
            string legacyContent = await File.ReadAllTextAsync(legacyFile);
            bool containsMarker = legacyContent.Contains(ModuleSpecifier, StringComparison.Ordinal);
            await File.WriteAllTextAsync(targetFile, containsMarker ? legacyContent : ModuleDeclaration);
            migrated = true;
            if (!containsMarker)
            {
                updated = true;
            }
        }

        if (!File.Exists(targetFile))
        {
            await File.WriteAllTextAsync(targetFile, ModuleDeclaration);
            added = true;
        }
        else
        {
            string existingContent = await File.ReadAllTextAsync(targetFile);
            if (!existingContent.Contains(ModuleSpecifier, StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(targetFile, ModuleDeclaration);
                updated = true;
            }
        }

        if (!File.Exists(scopeStubFile))
        {
            await File.WriteAllTextAsync(scopeStubFile, ScopeStub);
        }

        CleanupLegacy(legacyDir, legacyFile);

        return new TypeEnsureResult(added, migrated, updated);
    }

    internal static async Task<bool> EnsureTsConfigAsync(AppWorkspace workspace)
    {
        string tsconfig = workspace.WorkingPath.Combine(Files.BaseTsConfigJson);
        if (!File.Exists(tsconfig))
        {
            return false;
        }

        try
        {
            string json = await File.ReadAllTextAsync(tsconfig);
            JsonNode? root = JsonNode.Parse(json);
            if (root is not JsonObject obj)
            {
                return false;
            }

            if (obj["compilerOptions"] is not JsonObject compilerOptions)
            {
                compilerOptions = [];
                obj["compilerOptions"] = compilerOptions;
            }

            bool updated = false;

            if (compilerOptions["typeRoots"] is null)
            {
                JsonArray roots = [$"./{Folders.Types}", $"./{Folders.NodeModules}/@types"];
                compilerOptions["typeRoots"] = roots;
                updated = true;
            }

            string modulePath = $"./{Folders.Types}/{ScopeFolder}/{ModuleFolder}/{Files.Index}{FileExtensions.Dts}";

            if (compilerOptions["paths"] is not JsonObject paths)
            {
                paths = [];
                compilerOptions["paths"] = paths;
            }

            if (paths[ModuleSpecifier] is not JsonArray testPaths || testPaths.Count == 0)
            {
                paths[ModuleSpecifier] = new JsonArray(modulePath);
                updated = true;
            }

            if (!updated)
            {
                return false;
            }

            await File.WriteAllTextAsync(tsconfig, obj.ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Could not update {Files.BaseTsConfigJson}: {ex.Message}");
            return false;
        }
    }

    private static void CleanupLegacy(string legacyDir, string legacyFile)
    {
        if (!File.Exists(legacyFile))
        {
            return;
        }

        try
        {
            File.Delete(legacyFile);
            if (Directory.Exists(legacyDir) && !Directory.EnumerateFileSystemEntries(legacyDir).Any())
            {
                Directory.Delete(legacyDir);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Could not remove legacy types at {legacyDir}: {ex.Message}");
        }
    }
}

internal readonly record struct TypeEnsureResult(bool Added, bool Migrated, bool Updated);
