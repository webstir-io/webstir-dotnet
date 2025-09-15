# Code-Splitting MVP Plan

## Goal
Add basic code-splitting to Webstir using esbuild's native `--splitting` flag with minimal changes.

## Current State
- Per-page bundles with full duplication (shared libraries included in every page)
- Using esbuild for bundling via `EsbuildRunner.cs`
- HTML pages include single script tag per page

## MVP Scope

### What We're Building
1. Enable esbuild's automatic code-splitting with `--format=esm`
2. Parse the metafile to map entry points to their output files
3. Update HTML to include single entry point with `type="module"`
4. Let ESM handle chunk loading automatically
5. That's it.

### What We're NOT Building (yet)
- Custom chunk strategies
- Preloading/prefetching
- Dynamic imports
- Chunk analysis tools
- HTTP/2 push
- Loading states
- Error recovery

## Key Implementation Notes

### Critical: Don't Double-Hash!
- esbuild already hashes entries with `--entry-names=[dir]/index-[hash]`
- Just copy the entire esbuild output tree to dist
- Don't re-fingerprint after esbuild - it breaks chunk imports

### Critical: Preserve Structure
- Keep `--outbase` to maintain `pages/<page>/...` structure
- Copy chunks folder as-is to dist

### Change in Hashing Strategy
**Before (current):**
- esbuild produces unhashed files (`index.js`)
- Webstir's `FingerprintAndMoveAsync` computes and applies hashes
- All pipelines use Webstir's custom fingerprinting

**After (with code splitting):**
- JavaScript: esbuild handles hashing (via `--entry-names` and `--chunk-names`)
- CSS/Images/Fonts: Continue using Webstir's fingerprinting
- Why: esbuild must know final filenames when writing import statements between chunks

## Implementation

### Step 1: Enable Splitting in EsbuildRunner
```csharp
// Add to EsbuildRunner.BundleAsync()
if (mode == EsbuildMode.Production)
{
    args.Add("--splitting");
    args.Add("--format=esm");
    args.Add("--chunk-names=chunks/[name]-[hash]");
    args.Add("--entry-names=[dir]/index-[hash]");  // Hash entry files too
    args.Add($"--metafile={outDir}/meta.json");     // For manifest parsing
}
```

### Step 2: Parse Metafile and Update AssetManifest
```csharp
// In JsBuilder.cs, replace FingerprintAndMoveAsync with:
private async Task ProcessSplitBundlesAsync(string outDir)
{
    // Parse meta.json to find entry points
    var metafile = JsonSerializer.Deserialize<EsbuildMetafile>(
        await File.ReadAllTextAsync($"{outDir}/meta.json"));

    // Copy entire output tree to dist preserving structure
    CopyDirectory(outDir, _workspace.FrontendDistPath, recursive: true);

    // Update each page's manifest with hashed entry filename
    foreach (var output in metafile.Outputs.Where(o => o.EntryPoint != null))
    {
        var pageName = ExtractPageName(output.EntryPoint);
        var entryFileName = Path.GetFileName(output.Path);
        var pageDistDir = _workspace.FrontendDistPath.Combine("pages", pageName);

        AssetManifest.Update(pageDistDir, m => m.Js = entryFileName);
    }

    // Precompress all JS files (entries and chunks)
    await PrecompressJsFilesAsync(_workspace.FrontendDistPath);
}
```

### Step 3: HTML Already Works!
```csharp
// No changes needed in HtmlBundler!
// The template already has <script type="module" src="index.js">
// HtmlTransformer.RewriteAssetReferences will automatically
// rewrite it to the hashed entry from the manifest
```

## File Changes Required

### 1. `Constants.cs`
- Add `Folders.Chunks = "chunks"` constant

### 2. `JsConstants.cs`
- Add esbuild flag constants for consistency

### 3. `EsbuildRunner.cs`
- Add `--splitting`, `--chunk-names`, `--entry-names`, `--metafile` flags in production
- Keep `--format=esm` as-is

### 4. `JsBuilder.cs`
- Replace `FingerprintAndMoveAsync` with `ProcessSplitBundlesAsync`
- Parse metafile to find entry points
- Copy entire output tree preserving structure (no re-hashing!)
- Update AssetManifest with hashed entry filenames
- Precompress all JS files

### 5. `EarlyHintsMiddleware.cs`
- Read hashed filenames from page manifests instead of hardcoded `index.js`

## Testing Approach

### Manual Testing
1. Build a multi-page project with `publish` command
2. Verify `dist/frontend/chunks/` exists with JS files
3. Check HTML has a single script tag with hashed entry
4. Test in browser - pages should still work
5. Verify shared libraries are only downloaded once (check network tab)

### Automated Tests (using Tests runner)
```csharp
[Test]
public async Task Publish_WithCodeSplitting_CreatesChunks()
{
    // Build a multi-page template
    await RunPublishCommand(projectName);

    // Assert chunks folder exists
    Assert.IsTrue(Directory.Exists($"dist/frontend/chunks"));

    // Assert each page has hashed entry
    Assert.IsTrue(File.Exists($"dist/frontend/pages/index/index-*.js"));

    // Assert HTML contains hashed entry reference
    var html = await File.ReadAllTextAsync("dist/frontend/pages/index/index.html");
    Assert.IsTrue(html.Contains("type=\"module\""));
    Assert.IsTrue(html.Contains("/pages/index/index-"));
}
```

## Success Metrics

### Must Have (MVP)
- ✅ Code splits into vendor/page chunks
- ✅ HTML loads all required chunks
- ✅ Pages still work correctly
- ✅ Backwards compatible (can disable)

### Nice to Have (Post-MVP)
- Chunk size analysis
- Optimal chunk strategies
- Preloading critical chunks
- Dynamic import support

## Implementation Order

1. Update EsbuildRunner with splitting flags
2. Create metafile parser
3. Replace FingerprintAndMoveAsync with ProcessSplitBundlesAsync
4. Update EarlyHintsMiddleware to read from manifests
5. Add tests

## Rollout Strategy

1. **Default Behavior**: Code splitting always enabled in production mode
2. **Test Internally**: Run on internal projects first
3. **Ship It**: Just works, no configuration needed

## Example Output

### Before (Current)
```
dist/
├── pages/
│   ├── index/index.abc123.js (500KB - includes all dependencies)
│   └── about/index.def456.js (498KB - includes all dependencies)
```

### After (MVP)
```
dist/frontend/
├── chunks/
│   ├── vendor-789abc.js (150KB - third-party libs)
│   └── common-def123.js (20KB - shared app code)
├── pages/
│   ├── index/
│   │   ├── index-ghi789.js (8KB - page-specific only)
│   │   ├── index.html
│   │   └── manifest.json
│   └── about/
│       ├── index-jkl012.js (5KB - page-specific only)
│       ├── index.html
│       └── manifest.json
```

### HTML Before
```html
<script src="/pages/index/index.abc123.js"></script>
```

### HTML After
```html
<!-- Only the entry point - ESM loads chunks automatically -->
<script src="/pages/index/index-ghi789.js" type="module"></script>
```

The browser will automatically fetch the vendor and common chunks when it encounters the import statements in the entry module.

## Risk Mitigation

### Risk: Breaking existing projects
**Mitigation**: Extensive testing before release, production-only initially

### Risk: Module loading issues
**Mitigation**: Use type="module", test browser compatibility

### Risk: Performance regression
**Mitigation**: Measure before/after, only enable in production

## Post-MVP Enhancements

Once the MVP is working and stable:

1. Add chunk analysis reporting
2. Implement smart vendor chunk detection
3. Add preload tags for critical resources
4. Dynamic imports and lazy loading

## Decision Points

### Why esbuild --splitting?
- Already integrated
- Handles the hard parts (dependency graph, deduplication)
- Proven, battle-tested
- Zero additional dependencies

### Why production-only initially?
- Development mode works fine as-is
- Reduces testing surface
- Can expand later if needed

### Why no config file or escape hatch?
- Webstir is zero-config by design
- Code splitting should "just work"
- ESM + splitting is the correct default for production
- No reason to disable it
- Aligns with project philosophy

Note: If an emergency escape is needed during development, could use an environment variable (e.g., `WEBSTIR_DISABLE_SPLITTING=1`) but this shouldn't be documented or encouraged.

## Summary

This MVP delivers 80% of code-splitting value with 20% of the complexity. By leveraging esbuild's native splitting and ESM's automatic module loading, we can ship meaningful performance improvements quickly, then iterate based on real-world usage.

**Key insight from Codex**: With `--splitting` and `--format=esm`, we only need to inject the entry point script tag. The browser handles loading dependent chunks automatically through ESM imports - no need to manually inject all chunk script tags.

The key benefit: shared libraries (whatever they may be - framework code, utilities, third-party packages) are downloaded once and cached, rather than duplicated in every page bundle.