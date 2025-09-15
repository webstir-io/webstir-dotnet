# esbuild Integration Plan

This document outlines the plan to replace Webstir's custom JavaScript pipeline with esbuild, maintaining all current functionality while reducing maintenance burden and improving performance.

## Current State Analysis

### What We're Replacing
- `Engine/Pipelines/JavaScript/Parsing/JavaScriptParser.cs` - Custom AST parser
- `Engine/Pipelines/JavaScript/Transformation/JsModuleTransformer.cs` - Module graph resolution
- `Engine/Pipelines/JavaScript/Bundling/JsBundler.cs` - Custom bundling logic
- `Engine/Pipelines/JavaScript/Minification/JsMinifier.cs` - Custom minification
- `Engine/Pipelines/JavaScript/Minification/JsMinifyProcessor.cs` - Minification processor
- `Engine/Services/SourceMapService.cs` - Source map generation (partial)

### What We're Keeping
- `Engine/Pipelines/JavaScript/JsHandler.cs` - Orchestration layer (modified)
- TypeScript compilation via `tsc --build` (for type checking)
- Per-page bundling strategy
- Content-hash fingerprinting
- Manifest generation
- Development/production mode distinction

## Implementation Plan

### Step 1: Add esbuild
**Tasks:**
1. Create `Engine/Services/EsbuildService.cs`
   - Download/cache esbuild binary for platform
   - Handle process spawning and output capture
   - Error parsing and reporting

2. Replace JS pipeline in `JsHandler.cs`
   - Remove calls to custom parser/bundler/minifier
   - Call EsbuildService instead
   - Keep content hashing and manifest generation

### Step 2: Configure esbuild (Minimal)

**Development Mode:**
```bash
# Illustrative - actual implementation enumerates entries in C#
esbuild [enumerated entries] \
  --bundle --sourcemap --outdir=build/frontend --metafile=meta.json \
  --define:process.env.NODE_ENV=\"development\"
```

**Production Mode:**
```bash
# Illustrative - actual implementation enumerates entries in C#
esbuild [enumerated entries] \
  --bundle --minify --outdir=temp/frontend --metafile=meta.json \
  --define:process.env.NODE_ENV=\"production\" --drop:console
```

**Note:**
- Input from `build/frontend/pages/*/index.js` (tsc output), not `src/`
- Dev outputs directly to `build/frontend` (served by WebServer)
- Prod outputs to temp dir, then apply hex hashing and move to `dist/frontend`
- No source maps in production (unless explicitly enabled)
- C# enumerates entries and passes explicit paths via ArgumentList

### Step 3: Update Tests
- Update JavaScript test expectations for esbuild output
- Remove tests for custom parser/bundler/minifier components
- Add tests for EsbuildService

### Step 4: Delete Old Code

**Files to Remove:**
- `Engine/Pipelines/JavaScript/Parsing/` (entire directory)
- `Engine/Pipelines/JavaScript/Transformation/JsModuleTransformer.cs`
- `Engine/Pipelines/JavaScript/Bundling/JsBundler.cs`
- `Engine/Pipelines/JavaScript/Minification/` (entire directory)
- `Engine/Pipelines/JavaScript/Common/JsRegex.cs` (if unused)
- Related test files for removed components

**Files to Simplify:**
- `Engine/Pipelines/JavaScript/JsHandler.cs` - Remove branching logic
- `Engine/Services/SourceMapService.cs` - Rely on esbuild maps only

## Implementation Details

### EsbuildService.cs Structure
```csharp
public class EsbuildService
{
    private readonly string _binaryPath;
    private readonly ILogger<EsbuildService> _logger;
    private readonly AppWorkspace _workspace;

    public async Task<BundleResult> BundleAsync(BundleRequest request)
    {
        // 1. Enumerate page entries from build/frontend/pages/*/index.js
        // 2. Build args list with explicit paths via ArgumentList
        // 3. Dev: output to build/frontend; Prod: output to temp
        // 4. Parse metafile for dependencies
        // 5. Prod only: Apply hex content hashing
        // 6. Prod only: Move files to dist/frontend with hashed names
        // 7. Update manifest with filenames
    }

    public async Task EnsureBinaryAsync()
    {
        // Pin specific esbuild version
        // Download correct binary for platform
        // Cache in .tools/esbuild/
        // Allow override via environment variable
        // Fallback to npm package if needed
    }

    private DiagnosticCollection MapEsbuildErrors(string stderr)
    {
        // Parse esbuild output
        // Map to DiagnosticCollection with file/line/col
        // Normalize paths for cross-platform editor jumping
        // Match existing error format from tsc
    }
}
```

### Zero-Config Philosophy

**Core Principle:** Start with the absolute minimum configuration and only add flags when real-world usage demands it.

**Initial Implementation:**
- Development: `--bundle --sourcemap --define:process.env.NODE_ENV="development"`
- Production: `--bundle --minify --define:process.env.NODE_ENV="production" --drop:console`
- Dev outputs to `build/frontend`, Prod to temp then hash and move

**No Upfront Configuration For:**
- Target browsers (use esbuild's defaults)
- Module format (ESM is default)
- Platform (browser is default)
- Tree-shaking (automatic with minify)
- External dependencies (bundle everything initially)
- Code splitting (keep per-page bundles)
- JSX (only if/when needed)

**Handled Separately:**
- CSS processed by our existing CSS pipeline
- No CSS imports in JavaScript files

**Add Only When Needed:**
- User reports compatibility issue → add `--target`
- Bundle too large → add `--external` for specific packages
- Need code splitting → add `--splitting`
- Each flag added only after real usage validates the need

### Key Implementation Details

**1. CSS Handling**
- **Keep CSS pipeline separate** - our CSS bundler/minifier already works well
- No CSS imports in JavaScript files (maintain current practice)
- Document this constraint clearly for users
- If CSS-in-JS needed later, externalize CSS and handle via manifest

**2. Shared Error Script**
- Bundle with esbuild using specific entry point
- No source maps (esbuild won't add them without --sourcemap)
- Apply same hex content hashing

**3. Source Maps**
- Development: Use `--sourcemap` flag
- Production: No source maps by default
- Gate production maps behind env var or flag
- SourceMapMiddleware enforces access control

**4. Binary Management**
- Pin specific esbuild version (e.g., 0.19.x)
- Download correct binary for Darwin/Linux/Windows
- Cache in `.tools/esbuild/`
- Allow override via ESBUILD_BINARY_PATH env var
- Fall back to npm package if needed

**5. Content Hashing**
- Use our existing hex hasher (not esbuild's [hash])
- Maintains compatibility with cache regex expecting hex
- Apply after esbuild writes to temp directory

**6. Error Handling**
- Map esbuild diagnostics to DiagnosticCollection
- Preserve file/line/column information
- Match existing error format from tsc

## Success Criteria

- [ ] JavaScript tests pass with esbuild output
- [ ] Build time reduced by >50%
- [ ] Source maps work
- [ ] 2000+ lines of custom JS pipeline code deleted

## Timeline

- **Day 1-2**: Implement EsbuildService
- **Day 3**: Wire up to JsHandler, test
- **Day 4**: Update tests
- **Day 5**: Delete old code, cleanup

## Alternative Approaches Considered

1. **Rollup** - More mature, plugin ecosystem, but slower than esbuild
2. **SWC** - Rust-based, very fast, but less mature bundling
3. **Bun bundler** - Too new, requires Bun runtime
4. **Keeping custom** - Maintenance burden outweighs control benefits

## Decision: esbuild

**Reasons:**
- Exceptional performance (Go-based)
- Simplicity (single binary, no dependencies)
- Active maintenance
- Handles all modern JS features
- Used successfully by Vite, Remix, and others
- MIT licensed

## Next Steps

1. Implement EsbuildService
2. Replace JS pipeline
3. Update tests
4. Delete old code

## Notes

- Keep TypeScript compilation via `tsc --build` (outputs to build/)
- Bundle from tsc output, not source (avoids double transpilation)
- Keep per-page bundling strategy
- CSS stays in separate pipeline (no CSS imports in JS)
- Only add config flags when users need them