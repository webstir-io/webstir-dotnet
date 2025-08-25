# Phase 1.3: CSS Bundler Enhancement

## Overview
Extend the existing CSS handler with dependency tracking, CSS modules support, and PostCSS-like transformations without external tools.

## Goals
- Enhance CSS dependency tracking
- Implement CSS modules
- Add autoprefixer logic
- Build CSS minification
- Generate CSS source maps

## Technical Design

### Core Components

#### 1. CSS Module Graph
```csharp
public class CssModuleGraph
{
    private readonly Dictionary<string, CssModule> _modules = new();
    
    public class CssModule
    {
        public string FilePath { get; set; }
        public List<string> Imports { get; set; }
        public string Content { get; set; }
        public Dictionary<string, string> ClassMappings { get; set; }
        public string Hash { get; set; }
    }
    
    public async Task<CssBundle> BuildBundleAsync(string entryPoint)
    {
        var modules = await LoadModulesRecursively(entryPoint);
        var processed = ProcessModules(modules);
        var concatenated = ConcatenateStyles(processed);
        
        return new CssBundle
        {
            Css = concatenated,
            Mappings = ExtractMappings(modules)
        };
    }
}
```

#### 2. CSS Modules Implementation
```csharp
public class CssModulesProcessor
{
    public ProcessedCss ProcessModule(string css, string filePath)
    {
        var hash = GenerateHash(filePath);
        var ast = ParseCss(css);
        var mappings = new Dictionary<string, string>();
        
        foreach (var rule in ast.Rules)
        {
            if (IsLocalClass(rule))
            {
                var originalName = rule.Selector;
                var scopedName = $"{originalName}_{hash}";
                mappings[originalName] = scopedName;
                rule.Selector = scopedName;
            }
        }
        
        return new ProcessedCss
        {
            Css = GenerateCss(ast),
            Mappings = mappings
        };
    }
}
```

#### 3. Autoprefixer Implementation
```csharp
public class Autoprefixer
{
    private readonly Dictionary<string, List<string>> _prefixRules = new()
    {
        ["display: flex"] = ["-webkit-box", "-ms-flexbox"],
        ["transform"] = ["-webkit-transform", "-ms-transform"],
        ["user-select"] = ["-webkit-user-select", "-moz-user-select", "-ms-user-select"]
    };
    
    public string AddPrefixes(string css, string[] targetBrowsers)
    {
        var ast = ParseCss(css);
        
        foreach (var rule in ast.Rules)
        {
            foreach (var property in rule.Properties)
            {
                if (NeedsPrefix(property, targetBrowsers))
                {
                    AddVendorPrefixes(rule, property);
                }
            }
        }
        
        return GenerateCss(ast);
    }
}
```

## Implementation Steps

### Step 1: CSS Import Resolution
### Step 2: CSS Modules Support
### Step 3: Autoprefixer Logic
### Step 4: Advanced Minification
### Step 5: Source Map Generation

## Success Criteria
- ✅ Track CSS dependencies accurately
- ✅ CSS modules with scoped classes
- ✅ Automatic vendor prefixes
- ✅ Efficient minification
- ✅ Source maps for debugging