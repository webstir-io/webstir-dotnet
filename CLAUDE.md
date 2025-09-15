# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Important**: Also review the `.claude/` directory for additional project-specific instructions, style guidelines, and testing approaches.

## Commands

### Build and Test
```bash
# Build the entire solution
dotnet build Webstir.sln

# Format code and build (recommended before committing)
./utilities/format-build.sh

# Run tests (custom test runner, not xUnit/NUnit)
dotnet run --project Tests              # Quick tests (init, build, publish)
dotnet run --project Tests -- --full    # Full test suite (includes watch, help)
dotnet run --project Tests -- test init # Run specific test suite
```

### CLI Operations (from repo root)
```bash
# Initialize a new project
dotnet run --project CLI -- init <project-name>

# Start dev server with watch mode (default command)
dotnet run --project CLI -- watch --project-name <project-name>
dotnet run --project CLI -- --project-name <project-name>  # Same as watch

# Build project
dotnet run --project CLI -- build --project-name <project-name>

# Publish optimized production build
dotnet run --project CLI -- publish --project-name <project-name>

# Add a new page
dotnet run --project CLI -- add-page <page-name> --project-name <project-name>

# Run project tests
dotnet run --project CLI -- test --project-name <project-name>

# Get help
dotnet run --project CLI -- --help
```

### Build Published Binary
```bash
./publish.sh  # Creates single-file executable named 'webstir'
```

## Architecture

### Core Components

**Engine** - The heart of Webstir, containing:
- **Workflows**: Orchestrate build processes (InitWorkflow, BuildWorkflow, WatchWorkflow, PublishWorkflow, AddPageWorkflow, TestWorkflow)
- **Workers**: Handle specific build tasks (FrontendWorker, BackendWorker, SharedWorker)
- **Pipelines**: Process different asset types
  - HTML: Template assembly, minification
  - CSS & JavaScript: esbuild handles bundling, minification, and code-splitting
  - TypeScript: tsc for type checking, esbuild for bundling
  - Assets: Images, Fonts, Media handling
- **Servers**: WebServer (ASP.NET Core) and NodeServer for development
- **Services**: WatchService (file monitoring), ChangeService (SSE), DevService (orchestration)

**CLI** - Command-line interface that instantiates and runs workflows

**AppWorkspace** - Manages project directory structure and paths

### Project Structure
```
src/
├─ frontend/        # Client-side code
│  ├─ app/         # Base template (app.html, app.css, app.ts)
│  ├─ pages/       # Per-page components (index.html/css/ts)
│  └─ {images,fonts,media}/  # Static assets
├─ backend/        # Node.js server code (TypeScript)
└─ shared/         # Shared types between frontend and backend

build/             # Development build output
└─ frontend/       # Served by dev server on port 8088

dist/              # Production build output
└─ frontend/       # Optimized, fingerprinted assets
```

### Build Pipeline Flow

1. **TypeScript Compilation**: Single `tsc --build` for all TypeScript (frontend/backend/shared)
2. **HTML Assembly**: Merges page fragments with app.html template
3. **CSS & JavaScript Bundling** (via esbuild):
   - Unified pipeline for CSS and JavaScript processing
   - CSS: Resolves @import, processes CSS Modules, rewrites URLs
   - JavaScript: ESM bundling with automatic code-splitting
   - Dev: readable output with source maps
   - Publish: minified, tree-shaken, fingerprinted assets
4. **Asset Manifest**: Generated per page for cache-busted URLs

### Development Mode
- WebServer (port 8088): Serves frontend, proxies /api/* to NodeServer
- NodeServer (port 8008): Runs backend/index.js
- Live reload via Server-Sent Events (SSE)
- File watching with automatic rebuilds

### Key Design Decisions
- TypeScript-first with project references
- ESM-only (no CommonJS support)
- Per-page bundling strategy with automatic code-splitting
- esbuild for unified CSS/JS processing (10-100x faster)
- Deterministic outputs for given inputs
- Clear error messages with file/stage context

## Testing Approach

Tests use a custom runner (not xUnit/NUnit) located in Tests/Program.cs. Test suites cover each workflow (init, build, watch, publish, help, add). Tests can be run in quick mode (default) or full mode (includes longer-running tests).

## CSS Processing

CSS is now handled by esbuild alongside JavaScript bundling:
- **Development**: Bundled with source maps for debugging
- **Production**: Minified and fingerprinted
- **Features**:
  - Automatic @import resolution
  - CSS Modules support via .module.css extension
  - URL rewriting for assets
  - Modern CSS syntax including nesting
  - Unified pipeline with JavaScript for better performance

## Precompression Support

Engine/Pipelines/Core/Precompression.cs provides Brotli compression for published assets. Creates .br versions of CSS/JS files alongside originals for serving pre-compressed content.