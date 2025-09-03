using System.Text.RegularExpressions;

using Engine.Pipelines.JavaScript.Models;

namespace Engine.Pipelines.JavaScript.Publish;

public static class JsTreeShaker
{
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
}
