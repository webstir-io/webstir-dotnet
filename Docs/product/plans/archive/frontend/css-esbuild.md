# CSS Pipeline Migration to esbuild - MVP

## Executive Summary

Migrate Webstir's CSS pipeline to use the existing esbuild infrastructure (already used for JavaScript) to handle CSS bundling. This MVP focuses on core functionality with minimal changes.

## Current State

### Existing Infrastructure
- **EsbuildRunner.cs** - Already configured and working for JavaScript bundling
- **esbuild binary** - Already installed in node_modules and accessible
- **Process execution** - Proven pattern for running esbuild from C#

### Current CSS Pipeline
- Custom C# implementation with @import resolution, minification, and bundling
- CSS Modules support with `.module.css` files
- Content hashing for cache busting
- Precompression (Brotli/gzip)

## MVP Scope

### Core Features (Must Have)
- ✅ @import resolution and bundling
- ✅ CSS Modules support (`.module.css` files)
- ✅ CSS minification for production
- ✅ Content hashing
- ✅ Source maps for development

### Deferred Features (Post-MVP)
- ❌ Autoprefixing
- ❌ Legacy prefix stripping
- ❌ Font-face tweaks
- ❌ Critical CSS extraction

## Implementation Plan

### Step 1: Move EsbuildRunner to Core

Move `EsbuildRunner.cs` from `Engine/Pipelines/JavaScript/` to `Engine/Pipelines/Core/` since it will be shared by both JavaScript and CSS pipelines. Update namespaces accordingly.

### Step 2: Extend EsbuildRunner for CSS

Add CSS bundling method to the relocated `EsbuildRunner.cs`:

```csharp
public async Task BundleCssAsync(string entryPoint, string outFile, EsbuildMode mode, DiagnosticCollection? diagnostics = null)
{
    var args = new List<string>
    {
        Quote(entryPoint),
        "--bundle",
        "--loader:.css=css",
        "--loader:.module.css=local-css"  // CSS Modules support (requires esbuild 0.18.0+)
    };

    if (mode == EsbuildMode.Production)
    {
        args.Add("--minify");

        // Note: --entry-names doesn't work with --outfile
        // Build to temp file first, then rename with hash
        var tempFile = $"{outFile}.tmp";
        args.Add($"--outfile={Quote(tempFile)}");

        await ExecuteEsbuildAsync(args, diagnostics);

        // Calculate content hash and rename
        var hash = await CalculateFileHashAsync(tempFile);
        var hashedFile = Path.Combine(
            Path.GetDirectoryName(outFile),
            $"{Path.GetFileNameWithoutExtension(outFile)}.{hash}{Path.GetExtension(outFile)}"
        );
        File.Move(tempFile, hashedFile);
        return hashedFile;
    }
    else
    {
        args.Add("--sourcemap");
        args.Add($"--outfile={Quote(outFile)}");
    }

    await ExecuteEsbuildAsync(args, diagnostics);
    return outFile;
}
```

### Step 3: Update CssBuilder to Use EsbuildRunner

Modify the existing `CssBuilder` to use `EsbuildRunner` instead of custom processing:

```csharp
public class CssBuilder(AppWorkspace workspace, EsbuildRunner esbuildRunner)
{
    public async Task BuildAsync(DiagnosticCollection? diagnostics = null)
    {
        // For development builds, use esbuild for each CSS file
        foreach (var cssFile in GetPageCssFiles())
        {
            await esbuildRunner.BundleCssAsync(
                cssFile.Source,
                cssFile.Output,
                EsbuildMode.Development,
                diagnostics
            );
        }
    }
}
```

### Step 4: Update CssBundler for Production

Modify `CssBundler` to use `EsbuildRunner` for production bundling:

```csharp
public class CssBundler(AppWorkspace workspace, EsbuildRunner esbuildRunner)
{
    public async Task BundleAsync()
    {
        foreach (var pageCss in GetPageCssFiles())
        {
            // Use esbuild for production bundling
            await esbuildRunner.BundleCssAsync(
                pageCss.Source,
                pageCss.Output,
                EsbuildMode.Production
            );

            // Keep existing precompression and manifest updates
            await Precompression.CreatePrecompressedVariantsAsync(pageCss.Output);
            UpdateManifest(pageCss);
        }
    }
}
```

The `CssHandler` remains unchanged - it continues to orchestrate `CssBuilder` for development and `CssBundler` for production.

### Step 5: Testing

1. Test with existing CSS files
2. Verify @import resolution works
3. Check minification in production mode
4. Confirm content hashing works
5. Validate precompression still functions
6. **CSS Modules verification**:
   - Verify esbuild version is 0.18.0+ for `local-css` support
   - Compare class name generation patterns (esbuild default: `[local]_[hash]`)
   - Ensure scope isolation matches current behavior
   - Validate JSON export format if consumed by other systems

### Step 6: Validate and Monitor

1. Run existing test suite
2. Compare output sizes and performance
3. Document any differences

## Benefits

### Immediate Wins
- **Faster builds**: 10-100x improvement for CSS bundling
- **Less code**: Remove ~1000 lines of custom CSS processing
- **Unified tooling**: Same bundler for JS and CSS

### Future Benefits
- Easy to add CSS modules support later
- Can leverage esbuild plugins ecosystem
- Automatic performance improvements with esbuild updates

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Different @import behavior | Test thoroughly, document differences |
| Missing features | MVP scope excludes non-essential features |
| Unexpected output | Keep old implementation available for reference |
| Hash generation complexity | Post-process files for production hashing |
| CSS Modules compatibility | Verify class naming patterns match existing behavior |

## Success Criteria

1. All existing CSS files bundle correctly
2. Production builds are minified and hashed
4. All tests pass

## Implementation Order

1. Move EsbuildRunner to Core namespace
2. Extend EsbuildRunner with CSS bundling method
3. Update CssBuilder to use EsbuildRunner
4. Update CssBundler to use EsbuildRunner
5. Test and validate

## Next Steps (Post-MVP)

1. Implement autoprefixing with esbuild plugin or post-processing
2. Add legacy prefix stripping if needed
3. Consider removing legacy CSS pipeline after 1 week of stability
4. Add advanced features (font-face tweaks, critical CSS) as needed

## Conclusion

This MVP leverages existing esbuild infrastructure to quickly gain CSS bundling benefits with minimal risk and effort.