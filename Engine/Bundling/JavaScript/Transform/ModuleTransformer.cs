using Engine.Bundling.JavaScript.Graph;
using System.Text;

namespace Engine.Bundling.JavaScript.Transform;

public class ModuleTransformer(
    ScopeHoister scopeHoister,
    TreeShaker treeShaker)
{
    public TransformedModule Transform(ModuleInfo module, int moduleId, Dictionary<string, int> moduleIdMap)
    {
        StringBuilder output = new();
        
        output.AppendLine($"{TransformConstants.CommentPrefix} Module {moduleId}: {module.FilePath}");
        output.AppendLine($"{TransformConstants.Syntax.OpenParen}function{TransformConstants.Syntax.OpenParen}{TransformConstants.Syntax.CloseParen} {TransformConstants.Syntax.OpenBrace}");
        
        AppendImports(output, module.Imports, moduleIdMap);
        AppendModuleContent(output, module.Content);
        AppendExports(output, module.Exports, moduleId);
        
        output.AppendLine($"{TransformConstants.Syntax.CloseBrace}{TransformConstants.Syntax.CloseParen}{TransformConstants.Syntax.OpenParen}{TransformConstants.Syntax.CloseParen}{TransformConstants.Syntax.Semicolon}");
        
        string code = output.ToString();
        
        if (scopeHoister.CanHoist(module))
            code = scopeHoister.HoistScope(code, moduleId);
        
        if (treeShaker.HasUnusedExports(module))
            code = treeShaker.RemoveUnusedCode(code, module);
        
        return new TransformedModule
        {
            Id = moduleId,
            Code = code,
            SourceMap = null
        };
    }
    
    private static void AppendImports(StringBuilder output, List<ImportStatement> imports, Dictionary<string, int> moduleIdMap)
    {
        foreach (ImportStatement import in imports)
        {
            if (import.ResolvedPath == null || !moduleIdMap.TryGetValue(import.ResolvedPath, out int sourceModuleId))
                continue;
            
            if (import.DefaultSpecifier != null)
                output.AppendLine($"  {TransformConstants.Const} {import.DefaultSpecifier}{TransformConstants.Syntax.Assignment}{TransformConstants.GetModuleDefault(sourceModuleId)}{TransformConstants.Syntax.Semicolon}");
            
            foreach (string specifier in import.Specifiers)
                output.AppendLine($"  {TransformConstants.Const} {specifier}{TransformConstants.Syntax.Assignment}{TransformConstants.GetModuleExport(sourceModuleId, specifier)}{TransformConstants.Syntax.Semicolon}");
            
            if (import.NamespaceSpecifier != null)
                output.AppendLine($"  {TransformConstants.Const} {import.NamespaceSpecifier}{TransformConstants.Syntax.Assignment}{TransformConstants.GetModuleVar(sourceModuleId)}{TransformConstants.Syntax.Semicolon}");
        }
    }
    
    private static void AppendModuleContent(StringBuilder output, string content)
    {
        string cleanCode = RemoveImportsAndExports(content);
        output.AppendLine(cleanCode);
    }
    
    private static void AppendExports(StringBuilder output, List<ExportStatement> exports, int moduleId)
    {
        foreach (ExportStatement export in exports)
        {
            if (export.IsDefault)
                output.AppendLine($"  {TransformConstants.Var} {TransformConstants.GetModuleDefault(moduleId)}{TransformConstants.Syntax.Assignment}undefined{TransformConstants.Syntax.Semicolon}");
            
            foreach (string specifier in export.Specifiers)
                output.AppendLine($"  {TransformConstants.Var} {TransformConstants.GetModuleExport(moduleId, specifier)}{TransformConstants.Syntax.Assignment}{specifier}{TransformConstants.Syntax.Semicolon}");
        }
    }
    
    private static string RemoveImportsAndExports(string code)
    {
        code = TransformRegex.ImportStatement().Replace(code, string.Empty);
        code = TransformRegex.ExportKeyword().Replace(code, string.Empty);
        
        return code;
    }
}