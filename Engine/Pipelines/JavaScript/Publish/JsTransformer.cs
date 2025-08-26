using Engine.Pipelines.JavaScript.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace Engine.Pipelines.JavaScript.Publish;

public static class JsTransformer
{
    // Module transformation
    public static JsTransformedModule Transform(JsModuleInfo module, int moduleId, Dictionary<string, int> moduleIdMap)
    {
        StringBuilder output = new();
        
        output.AppendLine($"{Js.CommentPrefix} Module {moduleId}: {module.FilePath}");
        output.AppendLine($"{Syntax.OpenParen}function{Syntax.OpenParen}{Syntax.CloseParen} {Syntax.OpenBrace}");
        
        AppendImports(output, module.Imports, moduleIdMap);
        AppendModuleContent(output, module.Content);
        AppendExports(output, module.Exports, moduleId);
        
        output.AppendLine($"{Syntax.CloseBrace}{Syntax.CloseParen}{Syntax.OpenParen}{Syntax.CloseParen}{Syntax.Semicolon}");
        
        string code = output.ToString();
        
        if (CanHoist(module))
            code = HoistScope(code, moduleId);
        
        if (HasUnusedExports(module, []))
            code = RemoveUnusedCode(code, module, []);
        
        return new JsTransformedModule
        {
            Id = moduleId,
            Code = code,
            SourceMap = null
        };
    }
    
    private static void AppendImports(StringBuilder output, List<JsImportStatement> imports, Dictionary<string, int> moduleIdMap)
    {
        foreach (JsImportStatement import in imports)
        {
            if (import.ResolvedPath == null || !moduleIdMap.TryGetValue(import.ResolvedPath, out int sourceModuleId))
                continue;
            
            if (import.DefaultSpecifier != null)
                output.AppendLine($"  {Js.Const} {import.DefaultSpecifier}{Syntax.Assignment}{Js.GetModuleDefault(sourceModuleId)}{Syntax.Semicolon}");
            
            foreach (string specifier in import.Specifiers)
                output.AppendLine($"  {Js.Const} {specifier}{Syntax.Assignment}{Js.GetModuleExport(sourceModuleId, specifier)}{Syntax.Semicolon}");
            
            if (import.NamespaceSpecifier != null)
                output.AppendLine($"  {Js.Const} {import.NamespaceSpecifier}{Syntax.Assignment}{Js.GetModuleVar(sourceModuleId)}{Syntax.Semicolon}");
        }
    }
    
    private static void AppendModuleContent(StringBuilder output, string content)
    {
        string cleanCode = RemoveImportsAndExports(content);
        output.AppendLine(cleanCode);
    }
    
    private static void AppendExports(StringBuilder output, List<JsExportStatement> exports, int moduleId)
    {
        foreach (JsExportStatement export in exports)
        {
            if (export.IsDefault)
                output.AppendLine($"  {Js.Var} {Js.GetModuleDefault(moduleId)}{Syntax.Assignment}undefined{Syntax.Semicolon}");

            foreach (string specifier in export.Specifiers)
                output.AppendLine($"  {Js.Var} {Js.GetModuleExport(moduleId, specifier)}{Syntax.Assignment}{specifier}{Syntax.Semicolon}");
        }
    }
    
    private static string RemoveImportsAndExports(string code)
    {
        code = JsRegex.ImportStatement().Replace(code, string.Empty);
        code = JsRegex.ExportKeyword().Replace(code, string.Empty);
        
        return code;
    }
    
    // Scope hoisting
    public static bool CanHoist(JsModuleInfo module)
    {
        if (HasSideEffects(module.Content))
            return false;
        
        if (HasDynamicImports(module.Content))
            return false;
        
        if (UsesModuleGlobals(module.Content))
            return false;
        
        return true;
    }

    public static string HoistScope(string code, int moduleId)
    {
        string hoistedCode = RenameTopLevelDeclarations(code, moduleId);
        hoistedCode = RemoveModuleWrapper(hoistedCode);
        hoistedCode = OptimizeVariableDeclarations(hoistedCode);
        
        return $"// Module {moduleId} (hoisted)\n{hoistedCode}";
    }

    private static bool HasSideEffects(string code)
    {
        if (code.Contains("console."))
            return true;
        
        if (code.Contains("document."))
            return true;
        
        if (code.Contains("window."))
            return true;
        
        if (code.Contains("addEventListener"))
            return true;
        
        return false;
    }

    private static bool HasDynamicImports(string code)
    {
        return code.Contains("import(");
    }

    private static bool UsesModuleGlobals(string code)
    {
        if (code.Contains("__filename"))
            return true;
        
        if (code.Contains("__dirname"))
            return true;
        
        if (code.Contains("module."))
            return true;
        
        if (code.Contains("exports."))
            return true;
        
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
            result = JsRegex.IdentifierBoundary(rename.Key).Replace(result, rename.Value);

        return result;
    }

    private static string RemoveModuleWrapper(string code)
    {
        if (code.StartsWith("(function()", StringComparison.Ordinal))
            code = ExtractWrappedContent(code);
        
        return code;
    }

    private static string ExtractWrappedContent(string code)
    {
        int startIndex = code.IndexOf('{') + 1;
        int endIndex = code.LastIndexOf('}');
        
        if (startIndex > 0 && endIndex > startIndex)
            return code[startIndex..endIndex].Trim();
        
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
                pendingDeclarations.Add(line.Trim());
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
            result.Add(MergeDeclarations(pendingDeclarations));
        
        return string.Join('\n', result);
    }

    private static bool IsVariableDeclaration(string line)
    {
        string trimmed = line.Trim();
        return trimmed.StartsWith("const ", StringComparison.Ordinal) || 
               trimmed.StartsWith("let ", StringComparison.Ordinal) || 
               trimmed.StartsWith("var ", StringComparison.Ordinal);
    }

    private static string MergeDeclarations(List<string> declarations)
    {
        if (declarations.Count == 1)
            return declarations[0];
        
        List<string> constDecls = [];
        List<string> letDecls = [];
        
        foreach (string decl in declarations)
        {
            if (decl.StartsWith("const ", StringComparison.Ordinal))
                constDecls.Add(decl[6..].TrimEnd(';'));
            else if (decl.StartsWith("let ", StringComparison.Ordinal))
                letDecls.Add(decl[4..].TrimEnd(';'));
        }
        
        List<string> merged = [];
        
        if (constDecls.Count > 0)
            merged.Add($"const {string.Join(", ", constDecls)};");
        
        if (letDecls.Count > 0)
            merged.Add($"let {string.Join(", ", letDecls)};");
        
        return string.Join('\n', merged);
    }

    private static string RemoveUnusedVariables(string code)
    {
        return code;
    }
    
    // Tree shaking
    public static bool HasUnusedExports(JsModuleInfo module, HashSet<string> usedExports)
    {
        if (module.Exports.Count == 0)
            return false;
        
        foreach (JsExportStatement export in module.Exports)
        {
            if (!IsExportUsed(module.FilePath, export, usedExports))
                return true;
        }
        
        return false;
    }

    public static string RemoveUnusedCode(string code, JsModuleInfo module, HashSet<string> usedExports)
    {
        HashSet<string> unusedExports = GetUnusedExports(module, usedExports);
        
        if (unusedExports.Count == 0)
            return code;
        
        string result = code;
        
        foreach (string exportName in unusedExports)
            result = RemoveExport(result, exportName);
        
        result = RemoveOrphanedCode(result);
        
        return result;
    }
    
    private static bool IsExportUsed(string modulePath, JsExportStatement export, HashSet<string> usedExports)
    {
        if (export.IsDefault)
            return usedExports.Contains($"{modulePath}:default");
        
        if (export.Specifiers.Count > 0)
        {
            foreach (string name in export.Specifiers)
            {
                if (usedExports.Contains($"{modulePath}:{name}"))
                    return true;
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
                    unused.Add("default");
                else if (export.Specifiers.Count > 0)
                {
                    foreach (string name in export.Specifiers)
                        unused.Add(name);
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
    
    public static string Minify(string code)
    {
        code = JsRegex.SingleLineComment().Replace(code, string.Empty);
        code = JsRegex.MultiLineComment().Replace(code, string.Empty);
        code = JsRegex.ExcessiveWhitespace().Replace(code, " ");
        code = JsRegex.ExcessiveNewlines().Replace(code, "\n");
        code = JsRegex.EmptyLines().Replace(code, string.Empty);

        return code.Trim();
    }
}