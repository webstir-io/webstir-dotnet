# Webstir Bundler Conversion Plan

## Vision
Evolve Webstir into a simple, elegant web framework with a built-in bundler that requires zero configuration and no third-party dependencies. Focus on simplicity and approachability - anyone should be able to understand and use it without a steep learning curve.

## Core Principles
- **Dead Simple**: If it needs documentation, it's too complex
- **Zero Config**: Works out of the box with sensible defaults
- **No Dependencies**: Pure .NET implementation, no npm packages required
- **Smart Caching**: Content-based hashing for production, no caching in development
- **Fast Hot Reload**: Instant updates without cache conflicts
- **Vanilla First**: Embrace web standards, no complex abstractions
- **Learn in Minutes**: Anyone can be productive immediately

## Phase 1: Core Bundling Infrastructure

### 1.1 Module Graph Builder
- [ ] Create dependency graph analyzer for JS/TS modules
- [ ] Parse import/export statements using Regex patterns
- [ ] Build module resolution system (relative, absolute, bare imports)
- [ ] Implement circular dependency detection
- [ ] Track file dependencies for incremental builds

### 1.2 JavaScript/TypeScript Bundler
- [ ] Implement ES module concatenation
- [ ] Add scope hoisting for smaller bundles
- [ ] Create module wrapper for CommonJS compatibility
- [ ] Build source map generator
- [ ] Add tree shaking for dead code elimination

### 1.3 CSS Bundler Enhancement
- [ ] Extend existing CSS handler with dependency tracking
- [ ] Implement CSS modules support
- [ ] Add PostCSS-like transformations (autoprefixer logic)
- [ ] Create CSS minification without external tools
- [ ] Build CSS source maps

### 1.4 Asset Pipeline
- [ ] Implement asset fingerprinting with content hashes
- [ ] Create asset manifest for mapping original → hashed names
- [ ] Build inline asset optimization (data URIs for small files)
- [ ] Add image optimization (basic compression)
- [ ] Support font subsetting

## Phase 2: Cache Invalidation Strategy

### 2.1 Content Hashing System
```csharp
public class ContentHasher
{
    // SHA256 hash of file contents
    public string GenerateHash(byte[] content) => 
        Convert.ToBase64Url(SHA256.HashData(content))[..8];
    
    // Pattern: [name].[hash].[ext]
    // Example: app.a3f5c2d8.js
}
```

### 2.2 Development Mode (No Caching)
- [ ] Serve all assets with `Cache-Control: no-cache, no-store, must-revalidate`
- [ ] Add `Pragma: no-cache` and `Expires: 0` headers
- [ ] Timestamp query params on all script/link tags (`?t=1234567890`)
- [ ] Inject dev.js only in development mode
- [ ] WebSocket message types for targeted updates
- [ ] No filesystem caching - always serve fresh

### 2.3 Production Mode (Aggressive Caching)
- [ ] Generate content hashes for all assets
- [ ] Set `Cache-Control: public, max-age=31536000, immutable`
- [ ] Create manifest.json mapping original → hashed names
- [ ] Update HTML with hashed asset references
- [ ] Implement cascade invalidation (JS changes → HTML updates)

### 2.4 Smart Invalidation
```csharp
public class CacheInvalidator
{
    // When app.js changes:
    // 1. Generate new hash: app.a3f5c2d8.js → app.b7e4f9a2.js
    // 2. Update manifest.json
    // 3. Update index.html reference
    // 4. Old users get old bundle, new users get new bundle
    // 5. No manual cache clearing needed
}
```

## Phase 3: Bundle Optimization

### 3.1 Code Splitting
- [ ] Implement dynamic import() detection
- [ ] Create chunk generation for lazy-loaded modules
- [ ] Build runtime chunk loader
- [ ] Add prefetch/preload hints
- [ ] Support route-based splitting

### 3.2 Minification
- [ ] Build JavaScript minifier (remove whitespace, shorten variables)
- [ ] Implement CSS minifier (already partial support)
- [ ] Add HTML minification
- [ ] Create JSON minification
- [ ] Support conditional compilation (dev/prod code)

### 3.3 Compression
- [ ] Add Brotli compression support
- [ ] Implement gzip fallback
- [ ] Create pre-compressed assets
- [ ] Build compression cache
- [ ] Add dynamic compression for API responses

## Phase 4: Development Experience

### 4.1 Simple Hot Reload with dev.ts
- [ ] Create `src/client/app/dev.ts` - client-side dev service
- [ ] Page refresh on JS changes with state preservation
- [ ] CSS hot reload without page refresh
- [ ] Form state saved to sessionStorage before reload
- [ ] Scroll position preservation
- [ ] WebSocket auto-reconnect
- [ ] No complex HMR - just reliable reloads

#### Dev Service Architecture
```typescript
// src/client/app/dev.ts (copied from Engine/Templates during init)
interface DevMessage {
    type: 'reload' | 'css-update' | 'partial-update';
    file?: string;
}

class DevClient {
    private ws: WebSocket;
    
    constructor() {
        this.connect();
        this.restoreState();
    }
    
    private handleMessage(msg: DevMessage): void {
        switch (msg.type) {
            case 'css-update':
                this.updateCSS(msg.file!);  // No page reload
                break;
            case 'reload':
                this.saveState();  // Forms & scroll
                location.reload();
                break;
        }
    }
}
```

#### Server Integration
- **DevService.cs**: Detects file changes, sends typed messages
- **WebServer.cs**: Serves dev.js, handles WebSocket connections
- **Production**: dev.ts excluded from build automatically

### 4.2 Error Overlay
- [ ] Create in-browser error display
- [ ] Add compilation error formatting
- [ ] Implement runtime error capture
- [ ] Build stack trace mapper
- [ ] Add quick fix suggestions

### 4.3 Development Server
- [ ] Enhance WebSocket for HMR
- [ ] Add proxy configuration support
- [ ] Implement HTTPS with self-signed certs
- [ ] Create mock API endpoints
- [ ] Build request interceptor for debugging

## Phase 5: Zero Config Magic

### 5.1 Auto-Detection
```csharp
public class ProjectDetector
{
    public BundlerConfig Detect(string projectPath)
    {
        // Simple detection based on Webstir conventions
        // Pages: src/client/pages/*/index.{html,ts,css}
        // Shared components: src/shared/*.ts
        // Server API: src/server/*.ts
        // Everything just works with these conventions
    }
}
```

### 5.2 Convention over Configuration
- [ ] Source: `src/` directory
- [ ] Client entry: `src/client/app/app.ts`
- [ ] Dev service: `src/client/app/dev.ts` (dev only)
- [ ] Pages: `src/client/pages/*/index.{html,ts,css}`
- [ ] Output: `build/` (dev), `dist/` (production)
- [ ] Auto-detect TypeScript from tsconfig.json
- [ ] Auto-exclude dev.ts in production builds

### 5.3 Smart Defaults
- [ ] Development: Fast builds, no optimization
- [ ] Production: Full optimization, tree shaking
- [ ] Modern browsers: ES2020+ output
- [ ] Legacy support: Optional ES5 transpilation
- [ ] Source maps: On in dev, optional in prod

## Phase 6: CLI Simplification

### 6.1 Simple Command Structure
```bash
webstir          # Start dev server (that's it!)
webstir build    # Production build
webstir serve    # Preview production build
webstir new page-name  # Add a new page
```

### 6.2 Template Structure
```
Engine/Templates/        # Project templates copied during init
  ├── app/
  │   ├── app.ts
  │   ├── app.html
  │   ├── app.css
  │   └── dev.ts        # Dev service (client-side)
  └── pages/
      └── home/
          ├── index.html
          ├── index.ts
          └── index.css
```

### 6.3 Remove Complex Commands
- [ ] Remove workflow concept
- [ ] Simplify to just dev/build/serve
- [ ] Auto-detect everything else
- [ ] No configuration needed
- [ ] Optional webstir.json for overrides

## Implementation Priority

### Immediate (Week 1-2)
1. Content hashing system
2. Development no-cache headers
3. Production cache headers
4. Manifest generation
5. HTML reference updating

### Short Term (Week 3-4)
1. Module graph builder
2. Basic JS bundling
3. Enhanced CSS bundling
4. Asset fingerprinting
5. Hot reload improvements

### Medium Term (Week 5-8)
1. Code splitting
2. Minification
3. Tree shaking
4. HMR implementation
5. Error overlay

### Long Term (Week 9-12)
1. Advanced optimizations
2. Compression
3. Full zero-config
4. Plugin system (optional)
5. Performance profiling

## Success Metrics
- **Build Speed**: < 100ms incremental builds
- **Bundle Size**: 30% smaller than webpack default
- **Cache Hit Rate**: 95%+ for unchanged assets
- **Zero Config**: Works with no configuration file
- **Hot Reload**: < 50ms update time

## Migration Path
1. Keep existing framework mode with --legacy flag
2. New bundler mode as default
3. Gradual deprecation of framework features
4. Full bundler by v2.0

## Technical Decisions

### Why No NPM Dependencies?
- Faster installation (no node_modules)
- Better security (no supply chain attacks)
- Simpler deployment (single binary)
- More control over behavior
- No version conflicts

### Why Content Hashing?
- Guarantees cache invalidation on changes
- Enables immutable caching (1 year)
- No timestamp-based invalidation needed
- Works with CDNs perfectly
- No user intervention required

### Why No Config?
- Reduces cognitive load
- Faster project startup
- Fewer decisions to make
- Convention-based approach
- Override only when needed

## Risks & Mitigations
| Risk | Mitigation |
|------|------------|
| JS parsing complexity | Start with regex, evolve to AST if needed |
| Performance vs webpack | Focus on incremental builds, not initial |
| Missing ecosystem | Provide escape hatches for complex needs |
| TypeScript compilation | Use tsc as subprocess initially |
| Browser compatibility | Target modern browsers, add legacy mode later |

## Next Steps
1. Create proof of concept for content hashing
2. Implement basic module bundling
3. Test with real-world projects
4. Gather feedback from users
5. Iterate based on usage patterns