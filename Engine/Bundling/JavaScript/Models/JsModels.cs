namespace Engine.Bundling.JavaScript.Models;

public class BundleResult
{
    public required string Code { get; init; }
    public string? SourceMap { get; init; }
    public required string[] ModulePaths { get; init; }
}

// Module models
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

// Source map models
public class SourceMap
{
    public required int Version { get; init; }
    public required string[] Sources { get; init; }
    public required string[] Names { get; init; }
    public required string Mappings { get; init; }
}

public class MappingSegment
{
    public required int GeneratedLine { get; init; }
    public required int GeneratedColumn { get; init; }
    public required int SourceIndex { get; init; }
    public required int OriginalLine { get; init; }
    public required int OriginalColumn { get; init; }
    public int? NameIndex { get; init; }
}

// Transform models
public class TransformedModule
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public string? SourceMap { get; init; }
}

public class TransformContext
{
    public required int ModuleId { get; init; }
    public required string FilePath { get; init; }
    public required bool EnableScopeHoisting { get; init; }
    public required bool EnableTreeShaking { get; init; }
}