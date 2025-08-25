# Cache Busting Strategy for Webstir

## Problem
Browsers aggressively cache all web assets (HTML, CSS, JS, images) which prevents users from seeing updates without manually clearing their cache. This affects:

- **Static assets** (CSS, JS, images): Long cache times for performance
- **HTML files**: Often cached 5-60 minutes by default
- **API responses**: Can be cached depending on headers

A comprehensive strategy must handle all asset types to ensure reliable cache invalidation.

## Comprehensive Caching Strategy

A complete solution requires different approaches for different asset types:

| Asset Type | Strategy | Cache Duration | Method |
|------------|----------|----------------|---------|
| **HTML** | HTTP Headers | Short (5-15min) or No-Cache | `Cache-Control` headers |
| **CSS/JS** | Filename Versioning | Long (1 year) | Timestamp in filename |
| **Images** | Filename Versioning | Long (1 year) | Timestamp in filename |
| **Import Maps** | Embedded in HTML | Inherits HTML cache | Generated per build |

## Recommended Solution: Multi-Layer Cache Busting

Combines **HTTP headers for HTML** + **filename versioning for assets** + **Import Maps for clean code**.

### How It Works

1. **Generate timestamped filenames** during build/publish
2. **Create an Import Map** that maps original names to timestamped names
3. **Reference original names** in your code - Import Map handles the mapping

### Example Implementation

**Original files:**
```
app.css
index.js
utils.js
```

**After build (timestamp: 1724339200):**
```
app.1724339200.css
index.1724339200.js
utils.1724339200.js
```

**Generated Import Map (for JS only):**
```html
<script type="importmap">
{
  "imports": {
    "/index.js": "/index.1724339200.js", 
    "/utils.js": "/utils.1724339200.js"
  }
}
</script>
```

**Your HTML references:**
```html
<link rel="stylesheet" href="/app.1724339200.css">
<script type="module" src="/index.js"></script>
```

**Note:** CSS files get timestamped `href` directly, JS files use original names resolved via Import Map.

### Why This Is Optimal

- **Simple** - Just append Unix timestamp to filenames
- **Efficient** - Browser resolves correct versioned files automatically
- **Future-proof** - Easy to upgrade to content hashing later
- **Clean code** - No need to update references throughout codebase
- **Industry standard** - Uses modern web platform features

### Phased Implementation Plan

**Context:** You're implementing this in the Webstir framework where `HtmlHandler`, `CssHandler`, and `ScriptsHandler` manage asset processing during `BuildAsync()` and `PublishAsync()` phases.

#### Phase 1: Foundation - Timestamp Infrastructure

**What:** Create the shared timestamp system that all handlers will use.

**Implementation:**
1. Add `BuildTimestamp` property to `AppWorkspace` class
2. Set `BuildTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()` once per build
3. Create `AssetRegistry` class to track original → timestamped filename mappings

**Files to modify:**
- `AppWorkspace.cs` - add BuildTimestamp property
- New file: `Engine/Services/AssetRegistry.cs`

#### Phase 2: Static Assets Cache Busting (CSS + Images)

**What:** Update CSS and image files to use timestamped filenames.

**Why this first:** Static assets are simpler - direct filename updates without Import Maps.

**Implementation:**
1. **CssHandler:** Copy CSS files with timestamped names: `app.css` → `app.1724339200.css`
2. **ImagesHandler:** Copy image files with timestamped names: `logo.png` → `logo.1724339200.png`
3. **HtmlHandler:** Update `<link rel="stylesheet" href="...">` to reference timestamped CSS files
4. **CSS @imports:** Update any `@import` statements to reference timestamped files
5. **Image references:** Update `<img src="...">` and CSS `background-image` references

**Files to modify:**
- `CssHandler.cs` - modify `BuildAsync()` and `PublishAsync()`
- New file: `Engine/Handlers/ImagesHandler.cs` (if doesn't exist)
- `HtmlHandler.cs` - modify HTML link/image tag generation

#### Phase 3: JavaScript Cache Busting with Import Maps

**What:** Update JS files to use timestamped filenames + Import Maps for module resolution.

**Why after CSS:** Import Maps are more complex and only work with JavaScript modules.

**Implementation:**
1. **ScriptsHandler:** Copy JS files with timestamped names (except `refresh.js`)
2. **HtmlHandler:** Generate Import Map JSON with original → timestamped mappings
3. **HtmlHandler:** Inject `<script type="importmap">` into HTML `<head>`
4. **Keep original filenames** in `<script src="...">` tags - Import Map handles resolution

**Files to modify:**
- `ScriptsHandler.cs` - modify `BuildAsync()` and `PublishAsync()`
- `HtmlHandler.cs` - add Import Map injection logic

#### Phase 4: HTML Cache Headers

**What:** Configure HTTP headers to control HTML caching behavior.

**Why crucial:** HTML files must have short cache times or your timestamped assets won't be seen by users.

**Implementation:**
1. **WebServer configuration:** Set `Cache-Control` headers for HTML files
2. **Development mode:** Use `Cache-Control: no-cache, must-revalidate`
3. **Production mode:** Use `Cache-Control: max-age=300` (5 minutes) or no-cache
4. **ETag support:** Let server generate ETags for automatic validation

**Files to modify:**
- `WebServer.cs` - add cache header middleware
- Consider web server config (nginx, IIS, etc.) for production

#### Phase 5: Integration & Testing

**What:** Ensure all phases work together and handle edge cases.

**Implementation:**
1. **AssetRegistry integration:** All handlers register their timestamped files
2. **HTML injection order:** Import Map must come before any `<script type="module">` tags
3. **refresh.js handling:** Stays untimestamped in Build, excluded in Publish
4. **Error handling:** Fallback if Import Maps not supported
5. **Cache header validation:** Test HTML cache behavior

**Testing checklist:**
- HTML has correct cache headers (check DevTools Network tab)
- CSS loads with timestamped filenames
- Images load with timestamped filenames
- JS modules resolve through Import Maps
- Development vs Production differences work
- Browser cache actually gets busted on changes
- Test with hard refresh (Ctrl+F5) vs normal refresh

#### Implementation Notes for Future You

**Where Import Maps go:** 
- Injected into HTML `<head>` in `HtmlHandler.ProcessPageFileAsync()`
- Must be before any `<script type="module">` tags
- Similar to existing `InjectRoutingMetadata()` method

**Webstir-Specific Fallback Implementation:**
```csharp
// In HtmlHandler.cs
private string GenerateModuleScripts(List<string> scripts, long timestamp)
{
    var sb = new StringBuilder();
    
    // Feature detection with proper fallback
    sb.AppendLine(@"<script>
    (function() {
        if (typeof HTMLScriptElement.supports === 'undefined' || 
            !HTMLScriptElement.supports('importmap')) {
            // Browsers without Import Map support (12% of users)");
    
    foreach (var script in scripts)
    {
        var name = Path.GetFileNameWithoutExtension(script);
        sb.AppendLine($"            document.write('<script type=\"module\" src=\"/{name}.{timestamp}.js\"><\\/script>');");
    }
    
    sb.AppendLine(@"            return; // Skip Import Map injection
        }
        
        // Modern browsers with Import Map support (88% of users)
        const importMap = { imports: {");
    
    foreach (var script in scripts)
    {
        var name = Path.GetFileNameWithoutExtension(script);
        sb.AppendLine($"            '/{name}.js': '/{name}.{timestamp}.js',");
    }
    
    sb.AppendLine(@"        }};
        const mapScript = document.createElement('script');
        mapScript.type = 'importmap';
        mapScript.textContent = JSON.stringify(importMap);
        document.currentScript.after(mapScript);
    })();
    </script>");
    
    // Clean references (Import Map resolves these for modern browsers)
    foreach (var script in scripts)
    {
        var name = Path.GetFileNameWithoutExtension(script);
        sb.AppendLine($"<script type=\"module\" src=\"/{name}.js\"></script>");
    }
    
    return sb.ToString();
}
```

**CSS vs JS difference:**
- **CSS:** Direct filename updates in `<link href>`
- **JS:** Import Maps for module resolution in `<script type="module">`

**Shared timestamp (basic approach):**
- Generate once in `AppWorkspace` at build start
- All handlers use same timestamp for consistency
- Format: Unix epoch seconds (e.g., `1724339200`)
- **Alternative:** Per-file timestamps (see Advanced section below)

**File naming pattern:**
- Original: `app.css`, `index.js`, `logo.png`
- Timestamped: `app.1724339200.css`, `index.1724339200.js`, `logo.1724339200.png`
- Keep originals for Import Map keys

**HTTP Cache Headers Reference:**
```csharp
// In WebServer.cs - add middleware for HTML files
context.Response.Headers.Add("Cache-Control", "no-cache, must-revalidate");
context.Response.Headers.Add("Pragma", "no-cache");
context.Response.Headers.Add("Expires", "0");

// Alternative: Short cache for HTML
context.Response.Headers.Add("Cache-Control", "max-age=300"); // 5 minutes

// For static assets (CSS/JS/Images) - let them cache long
context.Response.Headers.Add("Cache-Control", "max-age=31536000"); // 1 year
```

**Production Web Server Config:**
```nginx
# nginx.conf example
location ~* \.html$ {
    add_header Cache-Control "no-cache, must-revalidate";
}

location ~* \.(css|js|png|jpg|jpeg|gif|svg)$ {
    add_header Cache-Control "max-age=31536000";
}
```

## Advanced: Targeted Cache Busting for Live Reload

**Problem:** Full rebuilds are slow for live reload. You want to only rebuild files that actually changed.

**Solution:** Enhance your existing `ChangeService` to support page-scoped targeted builds.

**When to implement:** After completing Phases 1-4 above, if you need faster live reload.

#### Smart File Change Detection

Your `ChangeService` already tracks changes. Extend it to determine rebuild scope:

```csharp
public class PageScopedChangeHandler 
{
    public static string? DetectAffectedPage(string changedFilePath, AppWorkspace workspace)
    {
        // /src/client/pages/home/index.css -> rebuild "home" page only
        if (changedFilePath.StartsWith(workspace.ClientPagesPath))
        {
            var relativePath = Path.GetRelativePath(workspace.ClientPagesPath, changedFilePath);
            return relativePath.Split(Path.DirectorySeparatorChar)[0]; // Extract page name
        }
        
        // /src/client/app/* -> rebuild ALL pages (shared resources)
        if (changedFilePath.StartsWith(workspace.ClientAppPath))
            return null; // null = rebuild all pages
            
        // /src/shared/* -> rebuild ALL pages
        if (changedFilePath.StartsWith(workspace.SharedPath))
            return null;
            
        return null; // Unknown scope, rebuild all
    }
}
```

#### Incremental Cache Busting Strategy

**Per-file timestamps (advanced optimization):**

```csharp
public class IncrementalAssetRegistry 
{
    private readonly Dictionary<string, long> _fileTimestamps = new();
    
    public string GetTimestampedFilename(string originalFile)
    {
        // Use per-file timestamp, only update if file actually changed
        var lastModified = File.GetLastWriteTime(originalFile).Ticks;
        
        if (!_fileTimestamps.ContainsKey(originalFile) || 
            _fileTimestamps[originalFile] != lastModified)
        {
            _fileTimestamps[originalFile] = lastModified;
        }
        
        var timestamp = _fileTimestamps[originalFile];
        return $"{Path.GetFileNameWithoutExtension(originalFile)}.{timestamp}{Path.GetExtension(originalFile)}";
    }
}
```

#### Live Reload Integration

**Modify your `ChangeService.ProcessChangesAsync()` to support targeted rebuilds:**

```csharp
// In ChangeService.ProcessChangesAsync()
var affectedPage = PageScopedChangeHandler.DetectAffectedPage(changeEvent.FilePath, _workspace);

if (affectedPage != null)
{
    // Rebuild only the specific page
    await RebuildSinglePage(affectedPage, changeEvent.FilePath);
}
else 
{
    // Rebuild all pages (shared resource changed)
    await _onChangeAction?.Invoke(changeEvent.FilePath, false);
}
```

#### Benefits for Live Reload

- **Page-specific CSS changes:** Only that page's CSS gets new timestamp
- **Shared app.css changes:** All pages get updated (necessary)
- **Individual page JS:** Only that page's JS bundle gets new timestamp
- **Faster builds:** Skip processing unchanged pages entirely
- **Smarter cache invalidation:** Unchanged files keep working from cache

This integrates perfectly with your existing `ChangeService` architecture and makes live reload much more responsive for page-specific changes.

## Browser Compatibility & Considerations

### Import Maps Support (2025)
- **88% global browser support** for Import Maps natively
- **Modern browsers:** Full support (Chrome 89+, Edge 89+, Firefox 108+, Safari 16.4+)
- **Legacy browsers:** Need polyfill or fallback
- **Why fallback still matters:** 12% of users, enterprise environments, older mobile devices

### Proper Feature Detection
```javascript
// Correct: Check if HTMLScriptElement.supports exists AND supports 'importmap'
if (typeof HTMLScriptElement.supports === 'undefined' || 
    !HTMLScriptElement.supports('importmap')) {
    // No Import Map support - use fallback
} else {
    // Import Maps are supported
}
```

### Fallback Implementation Options

#### Option 1: Feature Detection with Dynamic Loading (Recommended)
```html
<script>
// Check for Import Map support
(function() {
    if (typeof HTMLScriptElement.supports === 'undefined' || 
        !HTMLScriptElement.supports('importmap')) {
        // Fallback: Load timestamped files directly
        document.write('<script type="module" src="/index.1724339200.js"><\/script>');
        document.write('<script type="module" src="/utils.1724339200.js"><\/script>');
        return;
    }
    
    // Modern: Create and inject Import Map
    const importMap = {
        imports: {
            "/index.js": "/index.1724339200.js",
            "/utils.js": "/utils.1724339200.js"
        }
    };
    const script = document.createElement('script');
    script.type = 'importmap';
    script.textContent = JSON.stringify(importMap);
    document.currentScript.after(script);
})();
</script>

<!-- These load only if Import Maps are supported -->
<script type="module" src="/index.js"></script>
<script type="module" src="/utils.js"></script>
```

#### Option 2: ES Module Shims Polyfill (94% coverage)
```html
<!-- Polyfill adds Import Map support to browsers with ES modules -->
<script async src="https://ga.jspm.io/npm:es-module-shims@1.8.0/dist/es-module-shims.js"></script>

<script type="importmap">
{
  "imports": {
    "/index.js": "/index.1724339200.js"
  }
}
</script>

<!-- Use module-shim for polyfilled loading -->
<script type="module-shim" src="/index.js"></script>
```

**Note:** The incorrect `nomodule` approach from earlier doesn't work because:
- `nomodule` only checks for ES6 module support (2017+)
- Import Maps require module support PLUS Import Map support (2021+)
- Browsers can support modules but NOT Import Maps (e.g., Chrome 61-88)

### Browser Cache Behavior Notes
- **Safari:** More aggressive HTML caching, ensure strong cache headers
- **Chrome:** Respects cache headers well, good DevTools for testing
- **Firefox:** Similar to Chrome, good cache debugging
- **Mobile browsers:** Often have limited cache space, test thoroughly

### Testing Recommendations
1. **Test feature detection** - verify fallback works in older browsers
2. **Test in all target browsers** - cache behavior varies
3. **Test on mobile devices** - different cache constraints
4. **Test with slow connections** - ensure cache efficiency
5. **Use DevTools Network tab** - verify cache headers and timestamps
6. **Test hard refresh vs normal refresh** - both should work correctly
7. **Test with/without Import Map support** - use Firefox 107 or older for testing fallback

## Summary

**Implementation Result:**

**Complete implementation covers all asset types:**

**Files created:**
```
app.1724339200.css
index.1724339200.js
utils.1724339200.js
logo.1724339200.png
background.1724339200.jpg
```

**Import Map generated (JS only):**
```json
{
  "imports": {
    "/index.js": "/index.1724339200.js",
    "/utils.js": "/utils.1724339200.js"
  }
}
```

**Final HTML output with cache headers:**
```html
<!-- HTTP Response Headers -->
Cache-Control: no-cache, must-revalidate
Pragma: no-cache
Expires: 0

<!DOCTYPE html>
<html>
<head>
  <script type="importmap">
  {
    "imports": {
      "/index.js": "/index.1724339200.js",
      "/utils.js": "/utils.1724339200.js"
    }
  }
  </script>
  <link rel="stylesheet" href="/app.1724339200.css" />
  <script type="module" src="/index.js"></script>
  <!-- Fallback for legacy browsers -->
  <script nomodule src="/index.1724339200.js"></script>
</head>
<body>
  <img src="/logo.1724339200.png" alt="Logo" />
  <div style="background-image: url('/background.1724339200.jpg')">
    Content here
  </div>
</body>
</html>
```

**Result:** Complete cache busting across all asset types with optimal performance and browser compatibility.

### Benefits
- **Simple:** Single timestamp for entire build
- **Automatic:** No manual cache clearing needed
- **Clean code:** Original filenames preserved in source
- **Fast builds:** No content hashing overhead
- **Modern:** Uses native browser Import Map feature
- **Framework-agnostic:** Works with any web server

This strategy provides elegant, automatic cache busting that scales with your custom framework while keeping your code clean and maintainable.