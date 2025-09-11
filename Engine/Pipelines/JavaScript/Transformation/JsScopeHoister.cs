using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Engine.Pipelines.JavaScript.Common;
using Engine.Pipelines.JavaScript.Models;

namespace Engine.Pipelines.JavaScript.Transformation;

public static class JsScopeHoister
{
    public static bool CanHoist(JsModuleInfo module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (HasSideEffects(module.Content))
        {
            return false;
        }

        if (HasDynamicImports(module.Content))
        {
            return false;
        }

        if (UsesModuleGlobals(module.Content))
        {
            return false;
        }

        return true;
    }

    public static string HoistScope(string code, int moduleId)
    {
        ArgumentNullException.ThrowIfNull(code);
        string hoistedCode = RenameTopLevelDeclarations(code, moduleId);
        hoistedCode = RemoveModuleWrapper(hoistedCode);
        hoistedCode = OptimizeVariableDeclarations(hoistedCode);

        return FormattableString.Invariant($"// Module {moduleId} (hoisted)\n{hoistedCode}");
    }

    private static bool HasSideEffects(string code)
    {
        if (code.Contains("console.", StringComparison.Ordinal))
        {
            return true;
        }

        if (code.Contains("document.", StringComparison.Ordinal))
        {
            return true;
        }

        if (code.Contains("window.", StringComparison.Ordinal))
        {
            return true;
        }

        if (code.Contains("addEventListener", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool HasDynamicImports(string code) =>
        code.Contains("import(", StringComparison.Ordinal);

    private static bool UsesModuleGlobals(string code)
    {
        if (code.Contains("__filename", StringComparison.Ordinal))
        {
            return true;
        }

        if (code.Contains("__dirname", StringComparison.Ordinal))
        {
            return true;
        }

        if (code.Contains("module.", StringComparison.Ordinal))
        {
            return true;
        }

        if (code.Contains("exports.", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static string RenameTopLevelDeclarations(string code, int moduleId)
    {
        Dictionary<string, string> renames = [];

        foreach (Match match in JsRegex.FunctionDeclaration().Matches(code))
        {
            string originalName = match.Groups[1].Value;
            string newName = $"_m{moduleId}_{originalName}";
            renames[originalName] = newName;
        }

        foreach (Match match in JsRegex.ClassDeclaration().Matches(code))
        {
            string originalName = match.Groups[1].Value;
            string newName = $"_m{moduleId}_{originalName}";
            renames[originalName] = newName;
        }

        string result = code;
        foreach (KeyValuePair<string, string> rename in renames)
        {
            result = JsRegex.IdentifierBoundary(rename.Key).Replace(result, rename.Value);
        }

        return result;
    }

    private static string RemoveModuleWrapper(string code)
    {
        if (code.StartsWith("(function()", StringComparison.Ordinal))
        {
            code = ExtractWrappedContent(code);
        }

        return code;
    }

    private static string ExtractWrappedContent(string code)
    {
        int startIndex = code.IndexOf('{') + 1;
        int endIndex = code.LastIndexOf('}');

        if (startIndex > 0 && endIndex > startIndex)
        {
            return code[startIndex..endIndex].Trim();
        }

        return code;
    }

    private static string OptimizeVariableDeclarations(string code)
    {
        code = ConsolidateDeclarations(code);
        code = RemoveUnusedVariables(code);

        return code;
    }

    private static string ConsolidateDeclarations(string code)
    {
        List<string> lines = [.. code.Split('\n')];
        List<string> result = [];
        List<string> pendingDeclarations = [];

        foreach (string line in lines)
        {
            if (IsVariableDeclaration(line))
            {
                pendingDeclarations.Add(line.Trim());
            }
            else
            {
                if (pendingDeclarations.Count > 0)
                {
                    result.Add(MergeDeclarations(pendingDeclarations));
                    pendingDeclarations.Clear();
                }
                result.Add(line);
            }
        }

        if (pendingDeclarations.Count > 0)
        {
            result.Add(MergeDeclarations(pendingDeclarations));
        }

        return string.Join('\n', result);
    }

    private static bool IsVariableDeclaration(string line)
    {
        string trimmed = line.Trim();
        return trimmed.StartsWith("const ", StringComparison.Ordinal)
               || trimmed.StartsWith("let ", StringComparison.Ordinal)
               || trimmed.StartsWith("var ", StringComparison.Ordinal);
    }

    private static string MergeDeclarations(List<string> declarations)
    {
        if (declarations.Count == 1)
        {
            return declarations[0];
        }

        List<string> constDecls = [];
        List<string> letDecls = [];

        foreach (string decl in declarations)
        {
            if (decl.StartsWith("const ", StringComparison.Ordinal))
            {
                constDecls.Add(decl[6..].TrimEnd(';'));
            }
            else if (decl.StartsWith("let ", StringComparison.Ordinal))
            {
                letDecls.Add(decl[4..].TrimEnd(';'));
            }
        }

        List<string> merged = [];

        if (constDecls.Count > 0)
        {
            merged.Add($"const {string.Join(", ", constDecls)};");
        }

        if (letDecls.Count > 0)
        {
            merged.Add($"let {string.Join(", ", letDecls)};");
        }

        return string.Join('\n', merged);
    }

    private static string RemoveUnusedVariables(string code) => code;
}

