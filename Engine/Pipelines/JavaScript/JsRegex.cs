using System.Text.RegularExpressions;

namespace Engine.Pipelines.JavaScript;

public static partial class JsRegex
{
    // Import patterns
    [GeneratedRegex(@"import\s+(\w+)\s+from\s+['""]([^'""]+)['""]", RegexOptions.Multiline)]
    public static partial Regex DefaultImport();
    
    [GeneratedRegex(@"import\s*\{([^}]+)\}\s*from\s*['""]([^'""]+)['""]", RegexOptions.Multiline)]
    public static partial Regex NamedImports();
    
    [GeneratedRegex(@"import\s*\*\s*as\s+(\w+)\s+from\s+['""]([^'""]+)['""]", RegexOptions.Multiline)]
    public static partial Regex NamespaceImport();
    
    [GeneratedRegex(@"import\s+['""]([^'""]+)['""]", RegexOptions.Multiline)]
    public static partial Regex SideEffectImport();
    
    [GeneratedRegex(@"import\s*\(\s*['""]([^'""]+)['""]\s*\)", RegexOptions.Multiline)]
    public static partial Regex DynamicImport();
    
    [GeneratedRegex(@"import\s+(\w+)\s*,\s*\{([^}]+)\}\s*from\s*['""]([^'""]+)['""]", RegexOptions.Multiline)]
    public static partial Regex MixedImport();
    
    // Export patterns
    [GeneratedRegex(@"export\s+default\s+", RegexOptions.Multiline)]
    public static partial Regex DefaultExport();
    
    [GeneratedRegex(@"export\s*\{([^}]+)\}", RegexOptions.Multiline)]
    public static partial Regex NamedExports();
    
    [GeneratedRegex(@"export\s*\{([^}]+)\}\s*from\s*['""]([^'""]+)['""]", RegexOptions.Multiline)]
    public static partial Regex ReexportNamed();
    
    [GeneratedRegex(@"export\s*\*\s*from\s*['""]([^'""]+)['""]", RegexOptions.Multiline)]
    public static partial Regex ReexportAll();
    
    [GeneratedRegex(@"export\s*\*\s*as\s+(\w+)\s*from\s*['""]([^'""]+)['""]", RegexOptions.Multiline)]
    public static partial Regex ReexportNamespace();
    
    // Comment patterns
    [GeneratedRegex(@"//.*$", RegexOptions.Multiline)]
    public static partial Regex SingleLineComment();
    
    [GeneratedRegex(@"/\*[\s\S]*?\*/", RegexOptions.Multiline)]
    public static partial Regex MultiLineComment();
    
    // Transform patterns - Variable declarations
    [GeneratedRegex(@"\blet\b|\bconst\b|\bvar\b", RegexOptions.Multiline)]
    public static partial Regex VariableDeclaration();
    
    [GeneratedRegex(@"\bfunction\s+(\w+)", RegexOptions.Multiline)]
    public static partial Regex FunctionDeclaration();
    
    [GeneratedRegex(@"\bclass\s+(\w+)", RegexOptions.Multiline)]
    public static partial Regex ClassDeclaration();
    
    // Transform patterns - Function and class usage
    [GeneratedRegex(@"\b(\w+)\s*\(", RegexOptions.Multiline)]
    public static partial Regex FunctionCall();
    
    [GeneratedRegex(@"\bnew\s+(\w+)", RegexOptions.Multiline)]
    public static partial Regex ClassInstantiation();
    
    [GeneratedRegex(@"(?:const|let|var)\s+(\w+)\s*=\s*([^;]+);", RegexOptions.Multiline)]
    public static partial Regex VariableAssignment();
    
    // Transform patterns - Export patterns for removal
    [GeneratedRegex(@"export\s+default\s+[^;]+;")]
    public static partial Regex ExportDefaultStatement();
    
    [GeneratedRegex(@"export\s+default\s+function[^}]+}")]
    public static partial Regex ExportDefaultFunction();
    
    [GeneratedRegex(@"export\s+default\s+class[^}]+}")]
    public static partial Regex ExportDefaultClass();
    
    // Transform patterns - Whitespace cleanup
    [GeneratedRegex(@"\n\s*\n\s*\n")]
    public static partial Regex ExcessiveNewlines();
    
    [GeneratedRegex(@"\s{2,}")]
    public static partial Regex ExcessiveWhitespace();
    
    [GeneratedRegex(@"^\s*\n", RegexOptions.Multiline)]
    public static partial Regex EmptyLines();
    
    // Transform patterns - Import/Export removal
    [GeneratedRegex(@"^import\s+.*?;?\s*$", RegexOptions.Multiline)]
    public static partial Regex ImportStatement();
    
    [GeneratedRegex(@"^export\s+", RegexOptions.Multiline)]
    public static partial Regex ExportKeyword();
    
    // Dynamic export patterns (for building at runtime)
    public static Regex ExportNamedWithName(string exportName) =>
        new($@"export\s+{{\s*[^}}]*\b{System.Text.RegularExpressions.Regex.Escape(exportName)}\b[^}}]*\s*}};");
    
    public static Regex ExportVariableWithName(string exportName) =>
        new($@"export\s+(const|let|var)\s+{System.Text.RegularExpressions.Regex.Escape(exportName)}\s*=\s*[^;]+;");
    
    public static Regex ExportFunctionWithName(string exportName) =>
        new($@"export\s+function\s+{System.Text.RegularExpressions.Regex.Escape(exportName)}\s*\([^)]*\)\s*{{[^}}]+}}");
    
    public static Regex ExportClassWithName(string exportName) =>
        new($@"export\s+class\s+{System.Text.RegularExpressions.Regex.Escape(exportName)}\s*{{[^}}]+}}");
    
    public static Regex IdentifierBoundary(string identifier) =>
        new($@"\b{System.Text.RegularExpressions.Regex.Escape(identifier)}\b");
}