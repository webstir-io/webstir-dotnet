using Engine.Bundling.Graph;
using System.Text.RegularExpressions;

namespace Engine.Bundling.Transform;

public class TreeShaker
{
    private readonly HashSet<string> _usedExports = [];
    private readonly Dictionary<string, HashSet<string>> _moduleUsage = [];

    public void AnalyzeUsage(ModuleGraph graph)
    {
        _usedExports.Clear();
        _moduleUsage.Clear();
        
        foreach (string entryPoint in graph.GetEntryPoints())
            AnalyzeModule(entryPoint, graph);
    }

    public bool HasUnusedExports(ModuleInfo module)
    {
        if (module.Exports.Count == 0)
            return false;
        
        foreach (ExportStatement export in module.Exports)
        {
            if (!IsExportUsed(module.FilePath, export))
                return true;
        }
        
        return false;
    }

    public string RemoveUnusedCode(string code, ModuleInfo module)
    {
        HashSet<string> unusedExports = GetUnusedExports(module);
        
        if (unusedExports.Count == 0)
            return code;
        
        string result = code;
        
        foreach (string exportName in unusedExports)
            result = RemoveExport(result, exportName);
        
        result = RemoveOrphanedCode(result);
        
        return result;
    }

    private void AnalyzeModule(string modulePath, ModuleGraph graph)
    {
        ModuleNode? node = graph.GetModule(modulePath);
        if (node == null)
            return;
        
        if (!_moduleUsage.ContainsKey(modulePath))
            _moduleUsage[modulePath] = [];
        
        HashSet<string> usedIdentifiers = node.Info != null ? ExtractUsedIdentifiers(node.Info.Content) : [];
        _moduleUsage[modulePath] = usedIdentifiers;
        
        if (node.Info != null)
        {
            foreach (ImportStatement import in node.Info.Imports)
            {
                if (import.ResolvedPath == null)
                    continue;
                
                if (import.DefaultSpecifier != null)
                    _usedExports.Add($"{import.ResolvedPath}:default");
                
                foreach (string specifier in import.Specifiers)
                    _usedExports.Add($"{import.ResolvedPath}:{specifier}");
                
                if (import.NamespaceSpecifier != null)
                    _usedExports.Add($"{import.ResolvedPath}:*");
            }
        }
    }

    private static HashSet<string> ExtractUsedIdentifiers(string code)
    {
        HashSet<string> identifiers = [];
        
        foreach (Match match in TransformRegex.FunctionCall().Matches(code))
            identifiers.Add(match.Groups[1].Value);
        
        foreach (Match match in TransformRegex.ClassInstantiation().Matches(code))
            identifiers.Add(match.Groups[1].Value);
        
        foreach (Match match in TransformRegex.VariableAssignment().Matches(code))
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

    private bool IsExportUsed(string modulePath, ExportStatement export)
    {
        if (export.IsDefault)
            return _usedExports.Contains($"{modulePath}:default");
        
        if (export.Specifiers.Count > 0)
        {
            foreach (string name in export.Specifiers)
            {
                if (_usedExports.Contains($"{modulePath}:{name}"))
                    return true;
            }
            return false;
        }
        
        return false;
    }

    private HashSet<string> GetUnusedExports(ModuleInfo module)
    {
        HashSet<string> unused = [];
        
        foreach (ExportStatement export in module.Exports)
        {
            if (!IsExportUsed(module.FilePath, export))
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
            code = TransformRegex.ExportDefaultStatement().Replace(code, string.Empty);
            code = TransformRegex.ExportDefaultFunction().Replace(code, string.Empty);
            code = TransformRegex.ExportDefaultClass().Replace(code, string.Empty);
        }
        else
        {
            code = TransformRegex.ExportNamedWithName(exportName).Replace(code, string.Empty);
            code = TransformRegex.ExportVariableWithName(exportName).Replace(code, string.Empty);
            code = TransformRegex.ExportFunctionWithName(exportName).Replace(code, string.Empty);
            code = TransformRegex.ExportClassWithName(exportName).Replace(code, string.Empty);
        }
        
        return code;
    }

    private static string RemoveOrphanedCode(string code)
    {
        code = TransformRegex.ExcessiveNewlines().Replace(code, "\n\n");
        code = code.Trim();
        
        return code;
    }
}