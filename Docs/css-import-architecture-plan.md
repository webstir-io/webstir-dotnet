# CSS @import Architecture Plan

## Overview

This document outlines a plan to transition webstir from automatic CSS concatenation to explicit `@import` statements, giving developers full control over CSS bundling while maintaining build-time optimization.

## Current State vs Proposed Change

### Current State
- CSS files in `app/styles/` are automatically concatenated
- `app.css` is automatically included in every page
- Page CSS files are automatically appended
- Numbered files control order within directories

### Proposed Change
- All CSS dependencies use explicit `@import` statements
- Full control over what gets included and in what order
- Build-time processing optimizes differently for dev vs production
- Standard CSS syntax throughout

## Proposed Solution: CSS @import with Environment-Based Processing

Extend the current CSS bundling system to handle `@import` statements differently based on the environment:
- **Development (watch/build)**: Keep `@import` statements as-is for better debugging
- **Production (publish)**: Inline the imported CSS for optimal performance

### Syntax

```css
/* pages/about/about.css */
@import "@components/modal.css";
@import "@components/carousel.css";

.about-hero {
  /* regular page styles */
}
```

### Key Principles

1. **Standard CSS Syntax**: Uses familiar `@import` that developers already know
2. **Debug-Friendly Development**: Keeps imports separate in dev for easier debugging
3. **Optimized Production**: Inlines all CSS for single-file performance
4. **Namespace Support**: `@components/` provides clear separation from relative paths
5. **Progressive Enhancement**: Valid CSS that works even without processing

## Implementation Details

### Directory Structure

With full `@import` support, the directory structure remains flexible but imports are explicit:

```
app/
├── app.css                    # Main app file with @imports
├── styles/                    # Global styles (no auto-include)
│   ├── tokens.css            # Design tokens
│   ├── reset.css             # CSS reset  
│   ├── base.css              # Base element styles
│   └── utilities.css         # Utility classes
└── components/               # Reusable components
    ├── modal.css
    ├── carousel.css
    ├── tabs.css
    ├── accordion.css
    └── data-table.css

pages/
├── home/
│   ├── home.css              # Imports app.css and any needed components
│   └── styles/
│       ├── hero.css          
│       └── features.css      
└── about/
    ├── about.css             # Imports app.css and specific components
    └── styles/
        ├── team.css
        ├── 768-tablet.css    # Media queries still use numbers for order
        └── 1024-desktop.css
```

### StylesWorker Modifications

The `MergePageCssFiles` method will behave differently based on the command:

#### Build/Watch Commands
1. Parse CSS files for `@import` statements with namespaces
2. Rewrite the import paths to point to the build directory
3. Copy component files to build directory
4. Keep `@import` statements intact for browser debugging

#### Publish Command
1. Parse CSS files for `@import` statements
2. Resolve and read the component CSS
3. Replace `@import` statements with inlined content
4. Remove all import statements for optimal performance

#### Implementation in StylesWorker

The existing `Build()` and `Publish()` methods will handle imports differently:

```csharp
// In Build() method - keep imports as separate files
private string ProcessImportsForBuild(string cssContent, string appPath, string outputPath)
{
    // Regex to match @import statements
    var pattern = @"@import\s+(?:url\s*\()?\s*[""'](@[^""']+)[""']\s*\)?;";
    
    return Regex.Replace(cssContent, pattern, match =>
    {
        var importPath = match.Groups[1].Value;
        
        if (importPath.StartsWith("@components/"))
        {
            var componentName = importPath.Substring("@components/".Length);
            var sourcePath = Path.Combine(appPath, "components", componentName);
            
            if (File.Exists(sourcePath))
            {
                // Copy component to build directory
                var destPath = Path.Combine(outputPath, "components", componentName);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                File.Copy(sourcePath, destPath, true);
                
                // Rewrite import to use relative path from build
                return match.Value.Replace(importPath, $"../components/{componentName}");
            }
            else
            {
                return $"/* ERROR: Could not resolve {importPath} */";
            }
        }
        
        return match.Value;
    });
}

// In Publish() method - inline all imports
private string ProcessImportsForPublish(string cssContent, string appPath)
{
    // Regex to match @import statements
    var pattern = @"@import\s+(?:url\s*\()?\s*[""'](@[^""']+)[""']\s*\)?;";
    
    return Regex.Replace(cssContent, pattern, match =>
    {
        var importPath = match.Groups[1].Value;
        
        if (importPath.StartsWith("@components/"))
        {
            var componentName = importPath.Substring("@components/".Length);
            var componentPath = Path.Combine(appPath, "components", componentName);
            
            if (File.Exists(componentPath))
            {
                var componentCss = File.ReadAllText(componentPath);
                // Process any imports in the component file recursively
                componentCss = ProcessImportsForPublish(componentCss, appPath);
                return $"/* Inlined: {importPath} */\n{componentCss}";
            }
        }
        
        // Remove unresolved imports in publish
        return "";
    });
}
```

#### Namespace Resolution

The system supports special namespace prefixes for better organization:

```csharp
private Dictionary<string, string> namespaceMap = new Dictionary<string, string>
{
    { "@app/", "app/" },              // Points to app directory
    { "@components/", "app/components/" },
    { "@shared/", "shared/styles/" },
    { "@pages/", "pages/" }           // Cross-page imports
};
```

**Usage:**
- `@app/app.css` → `app/app.css`
- `@components/modal.css` → `app/components/modal.css`
- `@shared/mixins.css` → `shared/styles/mixins.css`
- `@pages/common/base.css` → `pages/common/base.css`

### Usage Examples

#### App-Level CSS
```css
/* app/app.css */
@import "./styles/tokens.css";
@import "./styles/reset.css";
@import "./styles/base.css";
@import "./styles/utilities.css";

/* Global app styles */
.app-container {
  max-width: 1200px;
  margin: 0 auto;
}
```

#### Basic Page
```css
/* pages/home/home.css */
@import "@app/app.css";
@import "./styles/hero.css";
@import "./styles/features.css";

.home-layout {
  /* page-specific styles */
}
```

#### Page with Components
```css
/* pages/products/products.css */
@import "@app/app.css";
@import "@components/carousel.css";
@import "@components/modal.css";
@import "@components/tabs.css";
@import "./styles/product-grid.css";
@import "./styles/768-tablet.css";
@import "./styles/1024-desktop.css";

.products-page {
  /* page-specific styles */
}
```

#### Component with Dependencies
```css
/* components/data-table.css */
@import "@components/table-base.css";
@import "@components/pagination.css";

.data-table {
  /* specific data table styles */
}
```

#### Media Query Files
```css
/* pages/about/styles/768-tablet.css */
@media (min-width: 768px) {
  /* Tablet styles - these are still auto-ordered by number */
}
```

### Bundle Output Examples

#### Build/Watch Output
In development, imports are rewritten to maintain separate files:

```css
/* build/client/products/products.css */
@import "../../app/app.css";
@import "../components/carousel.css";
@import "../components/modal.css";
@import "../components/tabs.css";
@import "./styles/product-grid.css";
@import "./styles/768-tablet.css";
@import "./styles/1024-desktop.css";

.products-page {
  /* page-specific styles */
}
```

The browser loads each file separately, preserving the development experience.

#### Publish Output
In production, all imports are recursively inlined into a single file:

```css
/* build/client/products/products.css */

/* Inlined: @app/app.css */
  /* Inlined: ./styles/tokens.css */
  :root { --color-primary: #0066cc; }
  
  /* Inlined: ./styles/reset.css */
  *, *::before, *::after { box-sizing: border-box; }
  
  /* Inlined: ./styles/base.css */
  body { font-family: system-ui; }
  
  /* Inlined: ./styles/utilities.css */
  .text-center { text-align: center; }
  
  .app-container { max-width: 1200px; margin: 0 auto; }

/* Inlined: @components/carousel.css */
.carousel { /* carousel styles */ }

/* Inlined: @components/modal.css */
.modal { /* modal styles */ }

/* Inlined: @components/tabs.css */
.tabs { /* tabs styles */ }

/* Inlined: ./styles/product-grid.css */
.product-grid { /* grid styles */ }

/* Inlined: ./styles/768-tablet.css */
@media (min-width: 768px) { /* tablet styles */ }

/* Inlined: ./styles/1024-desktop.css */
@media (min-width: 1024px) { /* desktop styles */ }

.products-page {
  /* page-specific styles */
}
```

## Migration Strategy

Since this is a breaking change, a careful migration path is needed:

### Phase 1: Parallel Support (Optional)
- Add `@import` processing while keeping automatic concatenation
- Allow projects to opt-in via configuration
- Test the new system with early adopters

### Phase 2: Migration Tools
- Provide a migration command: `webstir migrate-css`
- Auto-generate `@import` statements based on current structure
- Convert numbered files to explicit imports

### Phase 3: Full Transition
- Make `@import` the default in next major version
- Remove automatic concatenation code
- Update all documentation and examples

### Migration Example

**Before (Automatic):**
```
app/
├── app.css
└── styles/
    ├── 00-tokens.css    # Auto-included
    ├── 01-reset.css     # Auto-included
    └── 02-base.css      # Auto-included
```

**After (Explicit):**
```css
/* app/app.css */
@import "./styles/tokens.css";
@import "./styles/reset.css";
@import "./styles/base.css";
```

## Benefits

1. **Full Control**: Complete control over what CSS is included and in what order
2. **Standard Syntax**: Uses familiar CSS `@import` that developers already know
3. **Explicit Dependencies**: Clear visibility of all CSS dependencies in each file
4. **Better Debugging**: Build/watch keeps files separate for easier browser debugging
5. **Optimal Production**: Publish inlines everything for single-file performance
6. **Tree Shaking**: Unused CSS files are never included
7. **Modular Architecture**: Encourages better CSS organization and reusability
8. **External Resources**: Can eventually support external CSS imports
9. **Familiar Patterns**: Works like JavaScript module imports
10. **IDE Support**: Better autocomplete and go-to-definition for CSS files

## Potential Future Extensions

### Additional Namespaces
```css
@import "@shared/button.css";        /* From shared/styles/ */
@import "@vendor/flatpickr.css";     /* From node_modules/ */
@import "@app/utilities.css";        /* From app/styles/ */
```

### Relative Paths
```css
@import "../common/product-grid.css";     /* Relative to current page */
@import "./components/special-modal.css"; /* Page-specific variant */
```

### Import Ordering
```css
/* Imports are processed in order, allowing cascade control */
@import "@components/base-modal.css";
@import "@components/extended-modal.css"; /* Overrides base-modal */
```

### Nested Imports
Component files could also use `@import` for their dependencies:
```css
/* components/data-table.css */
@import "@components/table-base.css";
@import "@components/pagination.css";

.data-table {
  /* specific styles */
}
```

## Alternative Approaches Considered

### 1. File-Based Convention
Using marker files like `@modal.css` to signal imports. Rejected because:
- Creates empty files that serve no purpose
- Less visible than comments in the main CSS file
- Harder to see all dependencies at once

### 2. JavaScript-Driven Loading
Having components load their own CSS. Rejected because:
- Breaks the "CSS just works" philosophy
- Requires JavaScript for styling
- More complex implementation

### 3. Build-Time Analysis
Automatically detecting component usage. Rejected because:
- Too magical/implicit
- Hard to predict what will be included
- Complex to implement reliably

## Implementation Checklist

- [ ] Create `app/components/` directory structure in demo
- [ ] Modify `StylesWorker.MergePageCssFiles` to process imports
- [ ] Add import detection regex and parsing logic
- [ ] Handle missing components gracefully
- [ ] Add development mode warnings for missing components
- [ ] Test with demo project
- [ ] Update existing documentation
- [ ] Create migration guide for existing projects
- [ ] Add unit tests for import processing
- [ ] Consider performance implications

## Advantages Over Standard @import

While browsers support `@import` natively, webstir's approach provides several advantages:

1. **Best of Both Worlds**: Separate files for debugging, single file for production
2. **Performance**: Native `@import` causes sequential HTTP requests; webstir's publish inlines everything
3. **Namespaces**: Special paths like `@components/` provide better organization
4. **Smart Resolution**: Automatically handles path rewriting for build directory structure
5. **No Configuration**: Behavior changes automatically based on command (build vs publish)
6. **Error Handling**: Clear error messages when components aren't found

## Developer Experience

### During Development (build/watch)
- Browser DevTools show individual component files
- Easy to see which styles come from which component
- Can edit component CSS and see changes instantly
- Import errors show as comments in the CSS

### In Production (publish)
- Single optimized CSS file per page
- No runtime import overhead
- Smaller file size with comments removed
- Optimal performance

## Summary

Transitioning to full `@import` support represents a significant evolution in webstir's CSS architecture:

### What Changes
- **From**: Automatic concatenation with numbered ordering
- **To**: Explicit imports with full developer control

### Why It's Better
- **Predictability**: You see exactly what's included and in what order
- **Modularity**: Better code organization and reusability
- **Standards**: Uses native CSS syntax enhanced at build time
- **Performance**: Same optimized output, but with tree-shaking benefits

### Core Philosophy Maintained
While this is a breaking change, it still aligns with webstir's principles:
- **Standard syntax**: Real CSS `@import` statements
- **Build-time optimization**: Different behavior for dev vs production
- **Progressive enhancement**: Works without processing (browser tries to load)
- **Developer experience**: Better debugging in dev, optimized production builds

This change gives developers the control they expect from modern build tools while maintaining webstir's simplicity and performance benefits.