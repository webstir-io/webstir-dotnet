namespace Engine.Bundling.JavaScript.Graph;

public class ModuleInfo
{
    public required string FilePath { get; set; }
    public required string Content { get; set; }
    public ModuleType Type { get; set; }
    public List<ImportStatement> Imports { get; set; } = [];
    public List<ExportStatement> Exports { get; set; } = [];
}

public enum ModuleType
{
    ES6,
    CommonJS,
    TypeScript,
    Unknown
}

public class ImportStatement
{
    public required string Source { get; set; }
    public string? ResolvedPath { get; set; }
    public ImportType Type { get; set; }
    public List<string> Specifiers { get; set; } = [];
    public bool IsDynamic { get; set; }
    public int LineNumber { get; set; }
    public string? DefaultSpecifier { get; set; }
    public string? NamespaceSpecifier { get; set; }
}

public enum ImportType
{
    Default,
    Named,
    Namespace,
    SideEffect,
    Dynamic,
    Mixed
}

public class ExportStatement
{
    public ExportType Type { get; set; }
    public List<string> Specifiers { get; set; } = [];
    public string? Source { get; set; }
    public int LineNumber { get; set; }
    public bool IsDefault { get; set; }
}

public enum ExportType
{
    Default,
    Named,
    NamespaceReexport,
    AllReexport
}

public class ModuleNode
{
    public required string FilePath { get; set; }
    public HashSet<string> Dependencies { get; set; } = [];
    public HashSet<string> Dependents { get; set; } = [];
    public ModuleType Type { get; set; }
    public bool IsEntryPoint { get; set; }
    public ModuleInfo? Info { get; set; }
}

public class CircularDependency
{
    public List<string> Modules { get; set; } = [];
    public string Path { get; set; } = string.Empty;
}