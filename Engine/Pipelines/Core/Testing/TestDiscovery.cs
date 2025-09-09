using System;
using System.Collections.Generic;
using System.IO;

using Engine.Extensions;

namespace Engine.Pipelines.Core.Testing;

public sealed class TestDiscovery
{
    public static IEnumerable<string> FindSourceTests(AppWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        if (!Directory.Exists(workspace.SrcPath))
        {
            return [];
        }

        List<string> results = [];
        // Search for *.test.ts and *.test.js using constants
        string tsPattern = $"*{Files.Test}{FileExtensions.Ts}";
        string jsPattern = $"*{Files.Test}{FileExtensions.Js}";

        foreach (string pattern in new[] { tsPattern, jsPattern })
        {
            foreach (string file in Directory.EnumerateFiles(workspace.SrcPath, pattern, SearchOption.AllDirectories))
            {
                if (!IsUnderTestsFolder(file))
                {
                    continue;
                }

                if (IsExcluded(file))
                {
                    continue;
                }

                results.Add(file);
            }
        }

        results.Sort(StringComparer.Ordinal);
        return results;
    }

    public static IEnumerable<string> MapToCompiled(IEnumerable<string> sourceTests, AppWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(sourceTests);
        ArgumentNullException.ThrowIfNull(workspace);

        List<string> compiled = [];
        foreach (string source in sourceTests)
        {
            // Compute path relative to src root, then project to build root
            string relative = Path.GetRelativePath(workspace.SrcPath, source);
            string buildPath = workspace.BuildPath.Combine(relative);

            string ext = Path.GetExtension(buildPath);
            if (ext.Equals(FileExtensions.Ts, StringComparison.OrdinalIgnoreCase))
            {
                buildPath = Path.ChangeExtension(buildPath, FileExtensions.Js);
            }

            compiled.Add(buildPath);
        }

        compiled.Sort(StringComparer.Ordinal);
        return compiled;
    }

    private static bool IsUnderTestsFolder(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir))
        {
            string name = Path.GetFileName(dir);
            if (name.Equals(Folders.Tests, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return false;
    }

    private static bool IsExcluded(string filePath)
    {
        // Exclude node_modules, build, dist, and hidden folders
        char[] seps = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];
        string[] segments = filePath.Split(seps);
        foreach (string raw in segments)
        {
            string segment = raw.Trim();
            if (segment.Length == 0)
            {
                continue;
            }
            if (segment.StartsWith(".", StringComparison.Ordinal))
            {
                return true;
            }
            if (segment.Equals(Folders.NodeModules, StringComparison.OrdinalIgnoreCase)
                || segment.Equals(Folders.Build, StringComparison.OrdinalIgnoreCase)
                || segment.Equals(Folders.Dist, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
