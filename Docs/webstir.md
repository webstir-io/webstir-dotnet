# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Webstir is a minimalist fullstack framework written in C# (.NET 9.0) that combines static site generation with Node.js backend integration. It supports three project modes:
- **Fullstack** (default): Frontend + Node.js backend + shared types
- **Client-Only**: Static site generation only
- **Server-Only**: Node.js API server only

Key features include TypeScript compilation, hot reload, API proxy middleware, and automatic Node.js process management.

## Commands

### Getting Help
```bash
# Show all available commands
dotnet run -- help

# Show help for a specific command
dotnet run -- help init
dotnet run -- init --help

# Quick help
dotnet run -- --help
dotnet run -- -h
```

### Development
```bash
# Build and run the development server with hot reload (default command)
dotnet run -- watch
dotnet run --        # Same as watch

# Build the project to build directory
dotnet run -- build
dotnet run -- build --clean    # Clean build (removes build directory first)

# Create production build in dist directory
dotnet run -- publish

# Initialize a new project (defaults to fullstack)
dotnet run -- init
dotnet run -- init --client-only    # Frontend only
dotnet run -- init --server-only    # Backend only

# Add a new page (client-only and fullstack projects)
dotnet run -- add-page <page-name>
```

### Publishing the CLI tool
```bash
# Create a self-contained executable named 'webstir'
./publish.sh
```

## Command Reference

| Command | Description | Options |
|---------|-------------|---------|
| `help` | Show help information | `[command]` - Show help for specific command |
| `init` | Initialize a new webstir project | `--client-only` - Frontend only<br>`--server-only` - Backend only |
| `add-page` | Add a new page to your project | `<page-name>` - Name of the page (required) |
| `build` | Build the project once | `--clean` - Clean build directory first |
| `watch` | Build and watch for changes (default) | None |
| `publish` | Create production build | None |

## Architecture

### Build Pipeline Order
1. **Scripts** (BuildOrder: 1): TypeScript compilation for client and server
2. **Markup** (BuildOrder: 2): HTML fragment merging with app.html template
3. **Styles** (BuildOrder: 3): CSS consolidation (app.css + page-specific CSS)
4. **Images** (BuildOrder: 4): Asset copying

### Key Components
- **Runner.cs**: Command orchestrator handling init, add-page, build, publish, watch
  - Refactored with `IsHelpRequested()`, `ExecuteCommand()`, and `ShowUnknownCommandError()` methods
  - All command methods are now private for better encapsulation
- **Helper.cs**: Manages all help-related functionality
  - Contains command definitions with descriptions, usage, examples, and options
  - Provides colored console output for better readability
- **Commands.cs**: Centralized constants for all command names and options
  - No magic strings - all command names come from this single source
  - Enables easy rebranding by changing constants in one place
- **CommandHelp.cs**: Data model for command help information
- **Workers/**: Organized into Client/, Server/, and Shared/ subdirectories
  - Client: ScriptsWorker, MarkupWorker, StylesWorker, ImagesWorker (implement IPageWorker)
  - Server: ServerWorker
  - Shared: SharedWorker
- **WebServer.cs**: Kestrel-based static file server on port 8088
- **ApiProxy.cs**: Middleware forwarding /api/* requests to Node.js backend
- **NodeService.cs**: Node.js process management with auto-restart
- **Watcher.cs**: File system monitoring for automatic rebuilds

### Directory Structure (Fullstack)
```
src/
├── client/          # Frontend code
│   ├── app/         # Shared template files (app.html, app.css, app.ts)
│   ├── pages/       # Individual pages (each with index.html, index.css, index.ts)
│   └── images/      # Image assets
├── server/          # Backend Node.js code
│   └── index.ts     # Server entry point
└── shared/          # Shared types between client and server
    └── types/
        └── index.ts

build/               # Development builds
├── client/          # Compiled frontend (served by WebServer)
├── server/          # Compiled backend (run by NodeService)
└── shared/          # Compiled shared code

dist/                # Production builds (optimized, no refresh.js)
```

### Page Architecture
- Each page lives in `src/client/pages/<page-name>/`
- Page HTML fragments are merged into `src/client/app/app.html` template
- CSS files are concatenated: app.css + page-specific CSS
  - CSS files with numbers (e.g., `768.css`, `1024.css` for screen widths) are sorted numerically
  - The sorting extracts the first number from the filename and orders them ascending
  - This ensures media queries are applied in the correct cascade order
- TypeScript compiled to ES modules with imports resolved

### Fullstack Features
- **API Proxy**: Frontend requests to `/api/*` are forwarded to Node.js backend on port 3001
- **Shared Types**: TypeScript types can be shared between client and server
- **Process Management**: Node.js server automatically starts/stops/restarts with file changes
- **Unified Development**: Single command (`dotnet run -- watch`) runs both frontend and backend

### Hot Reload Implementation
- Development builds inject `refresh.js` into HTML
- WebSocket connection on port 3456 triggers page refresh on file changes
- Backend changes trigger automatic Node.js server restart
- Production builds strip out development scripts

### Port Configuration
- WebServer (static files): 8088
- Node.js API server: 3001
- WebSocket (hot reload): 3456