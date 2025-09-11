using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Engine.Pipelines.JavaScript.Models;

namespace Engine.Pipelines.JavaScript.Publish;

public static class JsTreeShaker
{
    // Usage analysis
    public static HashSet<string> AnalyzeUsage(JsModuleGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        HashSet<string> usedExports = [];

        foreach (string entryPoint in graph.GetEntryPoints())
            AnalyzeModule(entryPoint, graph, usedExports);

        return usedExports;
    }

    private static void AnalyzeModule(string modulePath, JsModuleGraph graph, HashSet<string> usedExports)
    {
        JsModuleNode? node = graph.GetModule(modulePath);
        if (node?.Info == null)
            return;

        foreach (JsImportStatement import in node.Info.Imports)
        {
            if (import.ResolvedPath == null)
                continue;

            if (import.DefaultSpecifier != null)
                usedExports.Add($"{import.ResolvedPath}:default");

            foreach (string specifier in import.Specifiers)
                usedExports.Add($"{import.ResolvedPath}:{specifier}");

            if (import.NamespaceSpecifier != null)
                usedExports.Add($"{import.ResolvedPath}:*");
        }
    }

    // Identifier extraction helpers
    public static HashSet<string> ExtractUsedIdentifiers(string code)
    {
        HashSet<string> identifiers = [];

        foreach (Match match in JsRegex.FunctionCall().Matches(code))
            identifiers.Add(match.Groups[1].Value);

        foreach (Match match in JsRegex.ClassInstantiation().Matches(code))
            identifiers.Add(match.Groups[1].Value);

        foreach (Match match in JsRegex.VariableAssignment().Matches(code))
        {
            string rightSide = match.Groups[2].Value;
            string[] tokens = rightSide.Split([' ', '.', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                if (IsIdentifier(token))
                    identifiers.Add(token);
            }
        }

        return identifiers;
    }

    private static bool IsIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        if (char.IsDigit(token[0]))
            return false;

        if (IsKeyword(token))
            return false;

        return token.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '$');
    }

    private static bool IsKeyword(string token)
    {
        HashSet<string> keywords = ["true", "false", "null", "undefined", "this", "new", "typeof", "instanceof"];
        return keywords.Contains(token);
    }

    // Export pruning
    public static bool HasUnusedExports(JsModuleInfo module, HashSet<string> usedExports)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(usedExports);
        if (module.Exports.Count == 0)
        {
            return false;
        }

        foreach (JsExportStatement export in module.Exports)
        {
            if (!IsExportUsed(module.FilePath, export, usedExports))
            {
                return true;
            }
        }

        return false;
    }

    public static string RemoveUnusedCode(string code, JsModuleInfo module, HashSet<string> usedExports)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(usedExports);
        HashSet<string> unusedExports = GetUnusedExports(module, usedExports);

        if (unusedExports.Count == 0)
        {
            return code;
        }

        string result = code;

        foreach (string exportName in unusedExports)
        {
            result = RemoveExport(result, exportName);
        }

        result = RemoveOrphanedCode(result);

        return result;
    }

    private static bool IsExportUsed(string modulePath, JsExportStatement export, HashSet<string> usedExports)
    {
        if (export.IsDefault)
        {
            return usedExports.Contains($"{modulePath}:default");
        }

        if (export.Specifiers.Count > 0)
        {
            foreach (string name in export.Specifiers)
            {
                if (usedExports.Contains($"{modulePath}:{name}"))
                {
                    return true;
                }
            }
            return false;
        }

        return false;
    }

    private static HashSet<string> GetUnusedExports(JsModuleInfo module, HashSet<string> usedExports)
    {
        HashSet<string> unused = [];

        foreach (JsExportStatement export in module.Exports)
        {
            if (!IsExportUsed(module.FilePath, export, usedExports))
            {
                if (export.IsDefault)
                {
                    unused.Add("default");
                }
                else if (export.Specifiers.Count > 0)
                {
                    foreach (string name in export.Specifiers)
                    {
                        unused.Add(name);
                    }
                }
            }
        }

        return unused;
    }

    private static string RemoveExport(string code, string exportName)
    {
        if (exportName == "default")
        {
            code = JsRegex.ExportDefaultStatement().Replace(code, string.Empty);
            code = JsRegex.ExportDefaultFunction().Replace(code, string.Empty);
            code = JsRegex.ExportDefaultClass().Replace(code, string.Empty);
        }
        else
        {
            code = JsRegex.ExportNamedWithName(exportName).Replace(code, string.Empty);
            code = JsRegex.ExportVariableWithName(exportName).Replace(code, string.Empty);
            code = JsRegex.ExportFunctionWithName(exportName).Replace(code, string.Empty);
            code = JsRegex.ExportClassWithName(exportName).Replace(code, string.Empty);
        }

        return code;
    }

    private static string RemoveOrphanedCode(string code)
    {
        code = JsRegex.ExcessiveNewlines().Replace(code, "\n\n");
        code = code.Trim();

        return code;
    }
}
