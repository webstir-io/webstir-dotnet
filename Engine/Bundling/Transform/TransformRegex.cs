using System.Text.RegularExpressions;

namespace Engine.Bundling.Transform;

public static partial class TransformRegex
{
    // Variable declarations
    [GeneratedRegex(@"\blet\b|\bconst\b|\bvar\b", RegexOptions.Multiline)]
    public static partial Regex VariableDeclaration();
    
    [GeneratedRegex(@"\bfunction\s+(\w+)", RegexOptions.Multiline)]
    public static partial Regex FunctionDeclaration();
    
    [GeneratedRegex(@"\bclass\s+(\w+)", RegexOptions.Multiline)]
    public static partial Regex ClassDeclaration();
    
    // Function and class usage
    [GeneratedRegex(@"\b(\w+)\s*\(", RegexOptions.Multiline)]
    public static partial Regex FunctionCall();
    
    [GeneratedRegex(@"\bnew\s+(\w+)", RegexOptions.Multiline)]
    public static partial Regex ClassInstantiation();
    
    [GeneratedRegex(@"(?:const|let|var)\s+(\w+)\s*=\s*([^;]+);", RegexOptions.Multiline)]
    public static partial Regex VariableAssignment();
    
    // Export patterns for removal
    [GeneratedRegex(@"export\s+default\s+[^;]+;")]
    public static partial Regex ExportDefaultStatement();
    
    [GeneratedRegex(@"export\s+default\s+function[^}]+}")]
    public static partial Regex ExportDefaultFunction();
    
    [GeneratedRegex(@"export\s+default\s+class[^}]+}")]
    public static partial Regex ExportDefaultClass();
    
    // Whitespace cleanup
    [GeneratedRegex(@"\n\s*\n\s*\n")]
    public static partial Regex ExcessiveNewlines();
    
    // Import/Export removal
    [GeneratedRegex(@"^import\s+.*?;?\s*$", RegexOptions.Multiline)]
    public static partial Regex ImportStatement();
    
    [GeneratedRegex(@"^export\s+", RegexOptions.Multiline)]
    public static partial Regex ExportKeyword();
    
    // Dynamic export patterns (for building at runtime)
    public static Regex ExportNamedWithName(string exportName) =>
        new($@"export\s+{{\s*[^}}]*\b{Regex.Escape(exportName)}\b[^}}]*\s*}};");
    
    public static Regex ExportVariableWithName(string exportName) =>
        new($@"export\s+(const|let|var)\s+{Regex.Escape(exportName)}\s*=\s*[^;]+;");
    
    public static Regex ExportFunctionWithName(string exportName) =>
        new($@"export\s+function\s+{Regex.Escape(exportName)}\s*\([^)]*\)\s*{{[^}}]+}}");
    
    public static Regex ExportClassWithName(string exportName) =>
        new($@"export\s+class\s+{Regex.Escape(exportName)}\s*{{[^}}]+}}");
    
    public static Regex IdentifierBoundary(string identifier) =>
        new($@"\b{Regex.Escape(identifier)}\b");
}