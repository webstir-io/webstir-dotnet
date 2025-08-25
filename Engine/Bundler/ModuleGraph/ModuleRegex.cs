using System.Text.RegularExpressions;

namespace Engine.Bundler.ModuleGraph;

public static partial class ModuleRegex
{
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
    
    [GeneratedRegex(@"//.*$", RegexOptions.Multiline)]
    public static partial Regex SingleLineComment();
    
    [GeneratedRegex(@"/\*[\s\S]*?\*/", RegexOptions.Multiline)]
    public static partial Regex MultiLineComment();
}