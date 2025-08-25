# Phase 1.1: Module Graph Builder

## Overview
Build a dependency graph analyzer for JavaScript/TypeScript modules to understand relationships between files and enable intelligent bundling.

## Goals
- Parse and analyze import/export statements
- Build a complete dependency graph
- Resolve module paths correctly
- Detect circular dependencies
- Enable incremental builds through dependency tracking

## Technical Design

### Core Components

#### 1. Module Parser
```csharp
public class ModuleParser
{
    // Parse imports/exports from file content
    public ModuleInfo ParseModule(string filePath, string content)
    {
        // Extract all import statements
        // Extract all export statements
        // Identify module type (ES6, CommonJS, etc.)
        // Return structured module information
    }
}
```

#### 2. Import Statement Patterns
```csharp
// ES6 Imports to detect:
// import defaultExport from "module-name"
// import * as name from "module-name"
// import { export1, export2 } from "module-name"
// import { export1 as alias1 } from "module-name"
// import "module-name" (side effects)
// Dynamic: import("module-name")

public static class ImportPatterns
{
    public static readonly Regex DefaultImport = 
        new(@"import\s+(\w+)\s+from\s+['""]([^'""]+)['""]", RegexOptions.Compiled);
    
    public static readonly Regex NamedImports = 
        new(@"import\s*\{([^}]+)\}\s*from\s*['""]([^'""]+)['""]", RegexOptions.Compiled);
    
    public static readonly Regex NamespaceImport = 
        new(@"import\s*\*\s*as\s+(\w+)\s+from\s+['""]([^'""]+)['""]", RegexOptions.Compiled);
    
    public static readonly Regex SideEffectImport = 
        new(@"import\s+['""]([^'""]+)['""]", RegexOptions.Compiled);
    
    public static readonly Regex DynamicImport = 
        new(@"import\s*\(\s*['""]([^'""]+)['""]\s*\)", RegexOptions.Compiled);
}
```

#### 3. Module Resolution
```csharp
public class ModuleResolver
{
    private readonly AppWorkspace _workspace;
    
    public string ResolvePath(string importPath, string fromFile)
    {
        // Handle different import types:
        // 1. Relative: ./module, ../module
        // 2. Absolute: /src/module
        // 3. Bare: lodash, react
        // 4. Index files: ./folder → ./folder/index.ts
        // 5. Extension resolution: ./file → ./file.ts, ./file.js
        
        if (IsRelativePath(importPath))
            return ResolveRelativePath(importPath, fromFile);
        
        if (IsAbsolutePath(importPath))
            return ResolveAbsolutePath(importPath);
        
        // Bare imports - check node_modules
        return ResolveBareImport(importPath);
    }
    
    private string[] Extensions = [".ts", ".tsx", ".js", ".jsx", ".json"];
}
```

#### 4. Dependency Graph
```csharp
public class ModuleGraph
{
    private readonly Dictionary<string, ModuleNode> _nodes = new();
    private readonly Dictionary<string, HashSet<string>> _edges = new();
    
    public class ModuleNode
    {
        public string FilePath { get; set; }
        public HashSet<string> Dependencies { get; set; } = new();
        public HashSet<string> Dependents { get; set; } = new();
        public DateTime LastModified { get; set; }
        public string ContentHash { get; set; }
        public ModuleType Type { get; set; } // ES6, CommonJS, etc.
    }
    
    public void AddModule(string filePath, IEnumerable<string> dependencies)
    {
        // Add or update module node
        // Update dependency edges
        // Update dependent relationships
    }
    
    public IEnumerable<string> GetAffectedModules(string changedFile)
    {
        // Return all modules that depend on the changed file
        // Used for incremental builds
    }
    
    public bool HasCircularDependency(string module)
    {
        // Depth-first search to detect cycles
    }
}
```

#### 5. Circular Dependency Detection
```csharp
public class CircularDependencyDetector
{
    private readonly ModuleGraph _graph;
    private readonly HashSet<string> _visited = new();
    private readonly HashSet<string> _recursionStack = new();
    
    public List<string> FindCircularDependencies()
    {
        var cycles = new List<string>();
        
        foreach (var module in _graph.GetAllModules())
        {
            if (DetectCycleFrom(module, new List<string>()))
            {
                // Build cycle path for error reporting
            }
        }
        
        return cycles;
    }
    
    private bool DetectCycleFrom(string module, List<string> path)
    {
        // DFS-based cycle detection
        // Track path for helpful error messages
    }
}
```

## Implementation Steps

### Step 1: Basic Import/Export Parsing
1. Create regex patterns for all import types
2. Build ModuleParser class
3. Test with sample TypeScript/JavaScript files
4. Handle edge cases (multiline imports, comments)

### Step 2: Module Resolution Logic
1. Implement relative path resolution
2. Add index file detection
3. Handle extension resolution
4. Support path aliases from tsconfig.json

### Step 3: Graph Construction
1. Create ModuleNode data structure
2. Build graph from parsed modules
3. Implement bidirectional relationships
4. Add content hashing for change detection

### Step 4: Circular Dependency Detection
1. Implement DFS-based detection
2. Build helpful error messages with cycle path
3. Add warnings for potential issues
4. Create visualization option

### Step 5: Incremental Build Support
1. Track file modifications
2. Implement affected module detection
3. Cache parsed module information
4. Optimize rebuild performance

## Data Structures

### ModuleInfo
```csharp
public class ModuleInfo
{
    public string FilePath { get; set; }
    public ModuleType Type { get; set; }
    public List<ImportStatement> Imports { get; set; }
    public List<ExportStatement> Exports { get; set; }
    public string ContentHash { get; set; }
    public DateTime LastModified { get; set; }
}

public class ImportStatement
{
    public string Source { get; set; }  // The module being imported from
    public ImportType Type { get; set; } // Default, Named, Namespace, etc.
    public List<string> Specifiers { get; set; } // What's being imported
    public bool IsDynamic { get; set; }
    public int LineNumber { get; set; }
}
```

## Testing Strategy

### Unit Tests
- Parse various import statement formats
- Test module resolution with different paths
- Verify circular dependency detection
- Test incremental build detection

### Integration Tests
- Build graph for real project
- Test with complex dependency trees
- Verify performance with large codebases
- Test edge cases (symlinks, case sensitivity)

## Performance Considerations

### Optimization Opportunities
1. **Parallel Parsing**: Parse multiple files concurrently
2. **Caching**: Cache parsed modules with content hash
3. **Lazy Loading**: Build graph incrementally as needed
4. **Memory Pool**: Reuse objects for large projects

### Benchmarks
- Target: Parse 1000 modules in < 100ms
- Graph construction: < 50ms for 1000 modules
- Incremental update: < 10ms per file change

## Error Handling

### Common Issues
1. **Missing modules**: Clear error with suggestion
2. **Circular dependencies**: Show cycle path
3. **Invalid syntax**: Graceful degradation
4. **File system errors**: Retry logic

### Error Messages
```csharp
public class ModuleGraphErrors
{
    public const string CircularDependency = 
        "Circular dependency detected: {0} → {1} → {0}";
    
    public const string ModuleNotFound = 
        "Cannot resolve module '{0}' from '{1}'";
    
    public const string InvalidImportSyntax = 
        "Invalid import statement at {0}:{1}";
}
```

## Integration Points

### With Bundler
- Provide dependency order for concatenation
- Identify entry points and chunks
- Support code splitting decisions

### With Watch Service
- Trigger graph updates on file changes
- Provide affected module list
- Optimize rebuild scope

### With Cache System
- Use content hash for cache keys
- Invalidate dependent modules
- Track build artifacts

## Success Criteria
- ✅ Parse all ES6 import/export syntax
- ✅ Resolve modules correctly
- ✅ Detect circular dependencies
- ✅ Support incremental builds
- ✅ Performance targets met
- ✅ Clear error messages
- ✅ 95% test coverage

## Future Enhancements
- Support for CommonJS require()
- TypeScript path mapping
- Webpack alias compatibility
- Plugin system for custom resolution
- Visual dependency graph output