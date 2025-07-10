# CLAUDE.md

This file provides implementation guidance to Claude Code when modifying the webstir codebase.

## Recent Changes (July 2025)

### Demo Command Implementation
Webstir now includes a `demo` command that creates a fully-featured demo application:

#### Command Features:
- **Auto-cleanup** - Automatically deletes existing demo folder for clean state
- **Auto-start** - Launches webstir development server after creation
- **Complete Example** - Showcases all webstir features in one app

#### Implementation:
- **ITemplateBuilder Interface** (`CLI/Interfaces/ITemplateBuilder.cs`)
  - Defines contract for template builders
  - Enables future template expansions
- **DemoBuilder** (`CLI/Builders/Demo/DemoBuilder.cs`)
  - Implements ITemplateBuilder
  - Uses embedded resources for template files
  - Calls webstir init first, then overlays demo files
- **Template Files** (`CLI/Builders/Demo/src/`)
  - Complete demo app source files
  - Uses @shared imports (no transformation needed)
  - Includes elegant minimal CSS styling

#### Key Files Created:
- **Client Pages**:
  - `home/` - Traditional navigation page
  - `about/` - SPA page with route handler
  - `features/` - SPA page demonstrating features
  - `api-demo/` - API integration showcase
- **Server**:
  - Uses native Node.js http module (no Express)
  - Demonstrates shared types usage
  - Includes user and data API endpoints
- **Shared Types**:
  - `types/index.ts` - Base ApiResponse type
  - `types/demo.ts` - Demo-specific types (User, Feature)

#### Build Integration:
- Template files included as EmbeddedResource in CLI.csproj
- Resource extraction handles file extensions correctly
- Demo folder added to .gitignore

#### Command Usage:
```bash
webstir demo              # Creates demo in 'demo' folder
webstir demo my-app       # Creates demo in 'my-app' folder
```

#### Runner.cs Implementation:
- Deletes existing demo directory if present
- Creates demo via DemoBuilder
- Starts webstir process in demo directory
- Uses Settings.DemoFolder constant ("demo")

## Recent Changes (July 2025)

### Client-Side Routing Implementation
Webstir now supports optional client-side routing for building SPAs:

#### New Files:
- **CLI/Resources/client/router.ts** - Core router implementation with navigation, link interception, and lifecycle management
- **CLI/Resources/client/navigation.ts** - Public navigation API for programmatic routing
- **CLI/Resources/shared/router-types.ts** - TypeScript interfaces shared between client and build system
- **CLI/Models/RoutingMetadata.cs** - C# models for routing configuration

#### Key Features:
- **Automatic Detection** - MarkupWorker detects pages that export `routeHandler`
- **Metadata Injection** - Routing metadata JSON injected into HTML in development mode
- **Dynamic Loading** - Router only loaded when SPA pages are detected
- **Lifecycle Hooks** - Support for onEnter, onLeave, and onUpdate handlers
- **Link Interception** - Automatic SPA navigation for internal links
- **Graceful Fallback** - Non-SPA pages continue using traditional navigation

#### Implementation Details:
- **MarkupWorker Changes**:
  - `DetectRoutingConfiguration()` - Scans pages for route handler exports
  - `InjectRoutingMetadata()` - Adds metadata JSON to HTML body
  - Routing metadata includes page names, routes, and SPA flags
- **App.ts Changes**:
  - Conditionally imports router based on metadata
  - Registers route handlers from page modules
  - Only initializes for projects with SPA pages
- **Router Features**:
  - Browser history management via History API
  - Query parameter extraction and passing to handlers
  - Prevents navigation loops and handles edge cases

## Recent Changes (July 2025)

### Help System Implementation
Webstir now has a comprehensive built-in help system:
- `webstir help` - Shows all available commands
- `webstir help <command>` - Shows detailed help for specific command
- `webstir <command> --help` or `-h` - Alternative syntax for command help
- All commands now have examples and option descriptions

Key implementation details:
- **Helper.cs** - Contains all help logic and command definitions
- **CommandHelp.cs** - Data model for command help information
- **Commands.cs** - Centralized constants for all command names and options
- No magic strings - all command names come from constants
- Colored output for better readability (cyan for commands, yellow for options)

### Code Organization Improvements
- **Runner.cs refactored** - Split into smaller methods: `IsHelpRequested()`, `ExecuteCommand()`, `ShowUnknownCommandError()`
- **Helper.cs location** - Moved from Services folder to main CLI folder alongside Runner.cs
- **Constants pattern** - All string literals extracted to `CLI/Constants/Commands.cs`
- **Private methods** - Command methods in Runner.cs are now private (better encapsulation)

### Project Modes
Webstir now supports three project modes via init command:
- `webstir init` - Fullstack (default): Creates client, server, and shared directories
- `webstir init --client-only` - Frontend only: No server or shared directories
- `webstir init --server-only` - Backend only: No client or shared directories

### Worker Organization
Workers are now organized into subdirectories:
- `CLI/Workers/Client/` - Client-side workers (Scripts, Markup, Styles, Images)
- `CLI/Workers/Server/` - Server-side workers (ServerWorker)
- `CLI/Workers/Shared/` - Shared workers (SharedWorker)

### Interface Changes
- Removed `InitOptions` class - now using `ProjectMode` enum directly
- `IFileWorker.Init()` now takes `ProjectMode` parameter with default value
- New `IPageWorker` interface extends `IFileWorker` for workers that create pages
- Removed unused interfaces: `IClientFileWorker`, `IServerFileWorker`, `ISharedFileWorker`

### Command Changes
- `webstir add <page-name>` renamed to `webstir add-page <page-name>` for clarity
- Only workers implementing `IPageWorker` are called for add-page command
- Default command is now `watch` - running `webstir` with no args starts the dev server
- Added `--clean` option to build command

### Directory Creation Fix
The `Directories` class now has helper methods to get directory info without auto-creating:
- `GetClientDirectory()` - Returns DirectoryInfo without creating
- `GetServerDirectory()` - Returns DirectoryInfo without creating  
- `GetSharedDirectory()` - Returns DirectoryInfo without creating

This prevents unwanted directory creation when checking project modes.

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
- Delegates help requests to Helper class
- Executes commands via private methods
- Manages build/watch lifecycle

**Helper.cs (CLI/Helper.cs)**
- Manages all help-related functionality
- Contains command definitions and metadata
- Provides `ShowGeneralHelp()` and `ShowCommandHelp()` methods
- Uses colored console output for better UX

**Commands.cs (CLI/Constants/Commands.cs)**
- Centralized location for all string constants
- Command names, option flags, and common strings
- Enables easy rebranding by changing constants in one place

**CommandHelp.cs (CLI/Models/CommandHelp.cs)**
- Data model for command help information
- Stores command name, description, usage, examples, and options

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

### Client Routing Details

**Route Handler Detection** (MarkupWorker.cs):
- Uses regex patterns to detect `export const routeHandler` in TypeScript files
- Supports multiple export syntaxes: named exports, default exports, object exports
- Only scans page TypeScript files, not app-level files

**Metadata Injection**:
- JSON metadata injected as `<script id="app-routing-metadata" type="application/json">`
- Only injected in development mode (not in production builds)
- Placed before closing `</body>` tag

**Router Initialization Flow**:
1. App.ts loads and checks for routing metadata in DOM
2. If SPA pages exist, dynamically imports router.ts
3. Router loads metadata and sets up browser navigation
4. For each SPA page, imports the page module and registers handlers

**Navigation Behavior**:
- Internal links with registered routes use pushState navigation
- External links, download links, and target="_blank" bypass router
- Non-SPA pages trigger full page loads
- Query parameters are extracted and passed to route handlers

**TypeScript Module Paths**:
- Client imports use relative paths or `@shared/` alias
- Shared types use `@shared/router-types.js` (note .js extension for ESM)
- Page modules expected at `../pages/{pageName}/{pageName}.js`

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