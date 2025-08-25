# Phase 1.2: JavaScript/TypeScript Bundler

## Overview
Implement a JavaScript/TypeScript bundler that concatenates ES modules, performs scope hoisting, and enables tree shaking for optimal bundle sizes.

## Goals
- Concatenate ES modules into single bundle
- Implement scope hoisting for smaller output
- Support CommonJS interop
- Generate accurate source maps
- Enable tree shaking for dead code elimination

## Technical Design

### Core Components

#### 1. Bundle Builder
```csharp
public class BundleBuilder
{
    private readonly ModuleGraph _graph;
    private readonly BundleOptions _options;
    
    public async Task<BundleResult> BuildBundleAsync(string entryPoint)
    {
        var modules = await LoadModulesInOrderAsync(entryPoint);
        var transformed = TransformModules(modules);
        var bundle = ConcatenateModules(transformed);
        var optimized = OptimizeBundle(bundle);
        
        return new BundleResult
        {
            Code = optimized.Code,
            SourceMap = optimized.SourceMap,
            Modules = modules.Select(m => m.FilePath)
        };
    }
}
```

#### 2. Module Transformer
```csharp
public class ModuleTransformer
{
    public TransformedModule Transform(ModuleInfo module, int moduleId)
    {
        var code = module.Content;
        
        code = TransformImports(code, module.Imports);
        code = TransformExports(code, module.Exports);
        
        if (CanHoistScope(module))
            code = HoistScope(code);
        else
            code = WrapInModuleScope(code, moduleId);
        
        return new TransformedModule
        {
            Id = moduleId,
            Code = code,
            SourceMap = GenerateSourceMap(module, code)
        };
    }
}
```

## Implementation Steps

### Step 1: Basic Module Concatenation
### Step 2: Import/Export Transformation  
### Step 3: Scope Hoisting
### Step 4: Tree Shaking
### Step 5: Source Maps

## Success Criteria
- ✅ Bundle ES6 modules correctly
- ✅ Support CommonJS interop
- ✅ Generate accurate source maps
- ✅ Scope hoisting reduces size by 30%
- ✅ Tree shaking removes unused code