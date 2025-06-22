# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Webstir is a minimalist static site generator framework written in C# (.NET 9.0) that compiles TypeScript and manages page-based static sites. It features a custom build pipeline, development server with hot reload, and a unique approach to merging page fragments with app-level templates.

## Commands

### Development
```bash
# Build and run the development server with hot reload
dotnet run -- watch

# Build the project to build/bin directory
dotnet run -- build

# Create production build in dist directory
dotnet run -- publish

# Initialize a new project
dotnet run -- init

# Add a new page
dotnet run -- add <page-name>
```

### Publishing the CLI tool
```bash
# Create a self-contained executable named 'webstir'
./publish.sh
```

## Architecture

### Build Pipeline Order
1. **Scripts** (BuildOrder: 1): TypeScript compilation via tsc
2. **Markup** (BuildOrder: 2): HTML fragment merging with app.html template
3. **Styles** (BuildOrder: 3): CSS consolidation (app.css + page-specific CSS)
4. **Images** (BuildOrder: 4): Asset copying

### Key Components
- **Runner.cs**: Command orchestrator handling init, add, build, publish, watch
- **Workers/**: File type handlers (MarkupWorker, StylesWorker, ScriptsWorker, ImagesWorker)
- **Server.cs**: Development server with WebSocket hot reload at http://localhost:8000
- **Watcher.cs**: File system monitoring for automatic rebuilds

### Directory Structure
```
src/
├── app/          # Shared template files (app.html, app.css, app.ts)
├── pages/        # Individual pages (each with index.html, index.css, index.ts)
└── images/       # Image assets

build/            # TypeScript output and development builds
└── bin/          # Development server root

dist/             # Production builds (no refresh.js)
```

### Page Architecture
- Each page lives in `src/pages/<page-name>/`
- Page HTML fragments are merged into `src/app/app.html` template
- CSS files are concatenated: app.css + page-specific CSS
  - CSS files with numbers (e.g., `768.css`, `1024.css` for screen widths) are sorted numerically
  - The sorting extracts the first number from the filename and orders them ascending
  - This ensures media queries are applied in the correct cascade order
- TypeScript compiled to ES modules with imports resolved

### Hot Reload Implementation
- Development builds inject `refresh.js` into HTML
- WebSocket connection on port 8000 triggers page refresh on file changes
- Production builds strip out development scripts