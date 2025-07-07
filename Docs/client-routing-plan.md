# Client Routing Plan - Zero Configuration Approach

## Overview

This plan outlines a zero-configuration routing system that automatically determines routing behavior based on conventions and file structure, eliminating the need for configuration files.

## Routing Modes (Auto-Detected)

### 1. **Multi-Page Application (MPA) Mode**
- **Detection**: Default mode when pages have no client-side route handlers
- **Behavior**: Traditional page loads, each page is a separate HTML file
- **Use case**: Content sites, blogs, documentation

### 2. **Single Page Application (SPA) Mode**
- **Detection**: Presence of `src/client/app/router.ts` file
- **Behavior**: Client-side routing handles all navigation
- **Use case**: Interactive applications, dashboards

### 3. **Hybrid Mode**
- **Detection**: Some pages export a `routeHandler` function
- **Behavior**: Mix of client-side and server-side routing
- **Use case**: Apps with both static content and interactive sections

## Convention-Based Architecture

### File Structure Conventions

```
src/client/
├── app/
│   ├── app.html      # Base template
│   ├── app.ts        # Global initialization
│   └── router.ts     # Optional: Enables SPA mode
├── pages/
│   ├── index/
│   │   ├── index.html
│   │   ├── index.ts
│   │   └── index.css
│   ├── about/
│   │   ├── index.html
│   │   ├── index.ts   # exports routeHandler = SPA page
│   │   └── index.css
│   └── blog/
│       ├── index.html
│       ├── index.ts   # no routeHandler = MPA page
│       └── index.css
└── components/       # Shared components
```

### Page Type Detection

Pages automatically become SPA-enabled by exporting a `routeHandler`:

```typescript
// MPA Page (blog/index.ts) - No special exports
console.log('Blog page loaded');

// SPA Page (about/index.ts) - Exports route handler
export const routeHandler = {
  onEnter: (params) => {
    // Called when navigating TO this page
  },
  onLeave: () => {
    // Called when navigating FROM this page
  },
  onUpdate: (params) => {
    // Called when route params change
  }
};
```

## Implementation Details

### 1. Build-Time Detection

The `MarkupWorker` will:
- Scan each page's TypeScript file for `routeHandler` export
- Generate routing metadata
- Inject appropriate scripts based on page type

### 2. Runtime Behavior

**MPA Pages**:
- No additional scripts injected
- Standard page navigation
- Each page loads fresh

**SPA Pages**:
- Lightweight router injected
- Intercepts navigation to SPA pages
- Falls back to standard navigation for MPA pages

**Hybrid Mode**:
- Router only handles pages with `routeHandler`
- Seamless transitions between modes

### 3. Router Implementation

Create minimal router (`src/client/app/router.ts`):

```typescript
// Presence of this file enables SPA capabilities
export class Router {
  private routes: Map<string, RouteHandler> = new Map();
  
  async navigate(path: string) {
    const handler = this.routes.get(path);
    if (handler) {
      // SPA navigation
      await this.loadPage(path);
    } else {
      // Fall back to standard navigation
      window.location.href = path;
    }
  }
}
```

### 4. Zero-Config Features

**Automatic Route Generation**:
- Routes match directory structure
- `/pages/about/` → `/about`
- `/pages/blog/post/` → `/blog/post`

**Smart Prefetching**:
- SPA pages can be prefetched
- MPA pages use standard browser prefetch

**Progressive Enhancement**:
- Works without JavaScript (MPA fallback)
- Enhances to SPA when JavaScript loads

## Benefits of Zero-Config Approach

1. **No Learning Curve**: Works like a traditional static site by default
2. **Opt-in Complexity**: Add SPA features only where needed
3. **Convention over Configuration**: Predictable behavior
4. **Incremental Adoption**: Convert pages to SPA one at a time
5. **Performance**: Only load router when needed

## Migration Path

### Phase 1: Foundation
1. Keep current MPA behavior as default
2. Add route detection to build process
3. Create minimal router module

### Phase 2: SPA Support
1. Implement client-side router
2. Add page transition handling
3. Support route parameters

### Phase 3: Advanced Features
1. Route guards and middleware
2. Nested routes
3. Code splitting for routes

## Example Usage

### Basic MPA Page
```typescript
// pages/contact/index.ts
// No special code needed - works as MPA
```

### SPA Page with Parameters
```typescript
// pages/product/index.ts
export const routeHandler = {
  onEnter: ({ id }) => {
    loadProduct(id);
  }
};

// Automatically handles /product/:id
```

### Hybrid Navigation
```typescript
// In any page
import { navigate } from '@webstir/router';

// Smart navigation - SPA or MPA based on target
navigate('/about');  // SPA transition
navigate('/blog');   // Standard navigation
```

## Technical Considerations

1. **SEO**: All pages pre-rendered at build time
2. **Performance**: Router only loaded when needed
3. **Compatibility**: Works in all browsers (with polyfills)
4. **TypeScript**: Full type safety for routes
5. **Hot Reload**: Maintains state during development

## Conclusion

This zero-configuration approach provides the flexibility of three routing modes without requiring any configuration. The system automatically adapts based on how developers structure their code, following the principle of least surprise while enabling powerful SPA features when needed.