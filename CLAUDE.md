# CLAUDE.md

This file provides implementation guidance to Claude Code when modifying the webstir codebase.

## Critical Implementation Details

### When Making Changes

1. **Port Numbers**: 
   - WebServer: 8088 (hardcoded in WebServer.cs)
   - Node.js API: 3001 (from webstir.json)
   - WebSocket: 3456 (for hot reload)

2. **Build Output Paths**:
   - Frontend: `build/client/` (NOT build/bin/)
   - Backend: `build/server/`
   - Shared: `build/shared/`
   - Static server root: `build/client/`

3. **Worker Responsibilities**:
   - **ScriptsWorker**: Only compiles `src/client/` TypeScript
   - **NodeJsWorker**: Compiles `src/server/` AND manages Node process
   - **MarkupWorker**: Merges HTML fragments, injects refresh.js in dev mode
   - Each worker has a BuildOrder property (1-5)

### Key Classes to Understand

**Runner.cs (CLI/Runner.cs)**
- Entry point for all commands
- Instantiates workers based on command
- Manages build/watch lifecycle

**WebServer.cs (CLI/Services/WebServer.cs)**
- Kestrel-based static file server
- Integrates ApiProxy middleware
- Serves from `build/client/` directory

**ApiProxy.cs (CLI/Services/ApiProxy.cs)**
- Middleware that intercepts `/api/*` requests
- Forwards to Node.js backend using HttpClient
- Preserves headers and request body

**NodeService.cs (CLI/Services/NodeService.cs)**
- Process management for Node.js
- Handles start/stop/restart
- Captures stdout/stderr
- Auto-restarts on file changes

**NodeJsWorker.cs (CLI/Workers/NodeJsWorker.cs)**
- Compiles server TypeScript using `tsc`
- Starts/stops NodeService
- Watches `src/server/**/*.ts` files

### TypeScript Compilation

**Client** (`src/client/tsconfig.json`):
```json
{
  "compilerOptions": {
    "outDir": "../../build/client",
    "rootDir": "../",
    "module": "esnext",
    "target": "es2020"
  }
}
```

**Server** (`src/server/tsconfig.json`):
```json
{
  "compilerOptions": {
    "outDir": "../../build/server",
    "rootDir": "../",
    "module": "commonjs",
    "target": "es2020"
  }
}
```

### HTML Generation Flow

1. MarkupWorker reads `src/client/app/app.html`
2. Finds `<!-- content -->` placeholder
3. For each page, inserts page's `index.html` content
4. In dev mode: injects `<script src="/refresh.js"></script>`
5. Outputs to `build/client/[page]/index.html`

### CSS Concatenation

1. StylesWorker reads `src/client/app/app.css`
2. For each page, appends page's `index.css`
3. Outputs to `build/client/[page]/index.css`

### Common Pitfalls

- Don't hardcode paths - use `Path.Combine()`
- Always check `Settings.IsProduction` before injecting dev scripts
- NodeService must be disposed properly to kill process
- Workers run in BuildOrder sequence (1-5)
- API proxy needs trailing slash handling

### Testing Changes

After modifying workers:
1. Run `dotnet run -- build` to test compilation
2. Run `dotnet run -- watch` to test full dev server
3. Check both frontend (8088) and API calls work
4. Verify Node.js process starts/stops correctly
5. Test hot reload by editing files

### File Watching Patterns

- Client TypeScript: `src/client/**/*.ts`
- Server TypeScript: `src/server/**/*.ts` 
- HTML: `src/client/**/*.html`
- CSS: `src/client/**/*.css`
- Shared types: `src/shared/**/*.ts`