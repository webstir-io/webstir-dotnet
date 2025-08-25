# Webstir Architecture

## Dependency Injection Setup
```csharp
// Program.cs configures all services
ServiceCollection services = new();
// Singletons: Long-lived services
services.AddSingleton<Runner, WatchService, ChangeService, DevService, WebServer, NodeServer>();
// Scoped: Per-command execution
services.AddScoped<AppWorkspace, IWorkflowFactory>();
// Transient: Stateless operations
services.AddTransient<[Handlers], [Workers], [Workflows]>();
```

## Workflow Execution Flow
1. **Runner.cs** receives command → creates scope → gets workspace & factory
2. **WorkflowFactory** creates appropriate workflow based on command
3. **BaseWorkflow** initializes workspace → executes workers
4. **Workers** run handlers in parallel based on BuildOrder
5. **Handlers** process specific file types

## Worker Orchestration
```csharp
// BaseWorkflow.cs - Workers run by BuildOrder, parallel within same order
BuildOrder:
  0: SharedWorker (shared types)
  1: ClientWorker, ServerWorker (parallel)
  2: Assets (images, handled within workers)
```

## File Change Processing
```
WatchService → FileSystemWatcher → OnChanged event
    ↓
ChangeService.EnqueueChange() → Queue<FileChange>
    ↓
DevService.ProcessChanges() → Determines affected workers
    ↓
Worker.BuildAsync(changedFile) → Incremental rebuild
    ↓
WebServer → Notifies browser via WebSocket
```

## Build Intelligence
- **Client changes**: Only ClientWorker runs
- **Server changes**: Only ServerWorker runs + Node restart
- **Shared changes**: Both workers run
- **Config changes**: Full clean build

## HTML Processing & SPA Detection
```csharp
// HtmlHandler.cs
1. Detect if router.ts exists → SPA mode
2. Check pages for routeHandler exports
3. Merge page HTML with app.html template
4. Inject routing metadata if SPA
5. Output to build/client/pages/[name]/index.html
```

## CSS Processing Pipeline
```csharp
// CssHandler.cs
1. Read CSS file
2. Process @import statements recursively
3. Resolve relative paths
4. Bundle into single file
5. Minify in production mode
```

## TypeScript Compilation
```csharp
// ScriptsHandler.cs
await ProcessRunner.RunAsync("npx", "tsc -p src/client/tsconfig.json");
// Uses --incremental flag with .tsbuildinfo for fast rebuilds
```

## Server Management (Fullstack Mode)
```csharp
// WebServer.cs - ASP.NET Core on port 8088
// NodeServer.cs - Node.js process on port 3001
// Proxy: /api/* requests → localhost:3001
```

## Project Mode Detection
```csharp
// AppWorkspace.DetectProjectMode()
(hasClient, hasServer) switch {
    (true, true) => ProjectMode.Fullstack,
    (true, false) => ProjectMode.ClientOnly,
    (false, true) => ProjectMode.ServerOnly,
    _ => ProjectMode.Legacy
}
```

## Templates Folder Structure
```
Engine/Templates/        → Embedded project templates
  ├── package.json       → Root package.json for new projects
  ├── base.tsconfig.json → Base TypeScript configuration
  └── src/
      ├── client/        → Client-side templates
      │   ├── app/       → Main app files (app.ts, app.html, app.css)
      │   ├── pages/     → Example page templates
      │   └── tsconfig.json
      ├── server/        → Server-side templates
      │   ├── index.ts   → Server entry point
      │   └── tsconfig.json
      └── shared/        → Shared code templates
          └── types/     → Shared TypeScript types
```
These templates are embedded as resources and copied to new projects during `init`.