# CLAUDE.md - Implementation Quick Reference

## Critical Constants
- **Ports**: WebServer: 8088, Node API: 3001, WebSocket: 3456
- **Build Paths**: Frontend: `build/client/`, Backend: `build/server/`, Dist: `dist/`
- **Entry Points**: Runner.cs → WorkflowFactory → Modules → Workers
- **Workspace**: Isolated workflows in `CLI/out/{workflow-name}/`

## Latest Features (July 2025)

### Module-Based Architecture ✨ NEW 
- **Smart Detection**: App auto-detects project type from folder structure
- **Intelligent Filtering**: Only loads relevant modules (Client/Server/Shared)
- **Project Types**: Fullstack, ClientOnly, ServerOnly
- **Performance**: Only runs workers needed for detected project type
- **Location**: App.cs:GetActiveModules(), Modules/{Client|Server|Shared}Module.cs

### Workflow System ✨ NEW
- **Convention-Based Factory**: Routes commands to workflows using DI
- **Isolated Workspaces**: Each workflow has dedicated `CLI/out/{name}/` directory
- **Parallel Execution**: Workers grouped by BuildOrder for optimal performance
- **Location**: WorkflowFactory.cs, Workflows/{Init|Build|Publish|AddPage}Workflow.cs

### CSS @import Support
- **Detection**: StylesWorker checks for @import in app.css
- **Build**: Preserves imports, copies files, rewrites paths
- **Publish**: Recursively inlines all imports
- **Namespaces**: @app/ → app/, @components/ → app/components/
- **Location**: StylesWorker.cs:195-309

### Client-Side Routing
- **Detection**: HtmlWorker scans for `export const routeHandler`
- **Metadata**: Injected as JSON in dev mode only
- **Location**: HtmlWorker.cs:169-219, router.ts, navigation.ts

## Architecture Patterns

### Module System (New)
- **ClientModule**: Contains HtmlWorker, StylesWorker, ScriptsWorker, ImagesWorker
- **ServerModule**: Contains ServerWorker for Node.js compilation
- **SharedModule**: Contains SharedWorker for TypeScript definitions
- **Auto-Loading**: App.GetActiveModules() filters by detected project type

### Workflow Execution (Optimized BuildOrder)
1. **ScriptsWorker** (BuildOrder 1): Heavy TypeScript compilation - gets full CPU
2. **SharedWorker** (BuildOrder 2): Fast type definitions
3. **Parallel Group** (BuildOrder 3): HtmlWorker + StylesWorker + ImagesWorker + ServerWorker
   - Total execution groups: 3 (down from 6 sequential)
   - Performance: 5x parallelization where beneficial

### Key Interfaces
- **IWorkflow**: Base for all workflows (ExecuteAsync with parameters)
- **IAppModule**: Module container with Workers collection
- **IModuleWorker**: Base for all workers (Init, Build, Publish, AddPage)
- **IWorkflowFactory**: Convention-based command routing

## Common Pitfalls
- **Path.Combine**: Use for all paths, never hardcode
- **Workspace Paths**: App automatically points to workflow workspace after InitializeWorkspace()
- **BuildOrder Optimization**: Only parallelize when multiple heavy operations exist
- **Module Registration**: All worker interfaces must be registered in DI for modules to load
- **Resource Paths**: Must match `CLI.Resources.{path}.{file}` pattern

## Performance Guidelines
- **BuildOrder 1**: Heavy operations that benefit from full CPU (TypeScript compilation)
- **BuildOrder 2**: Fast operations with dependencies
- **BuildOrder 3+**: Parallel groups of independent operations
- **Rule**: Don't parallelize unless multiple slow operations exist (avoid overhead)

## Testing Checklist
```bash
dotnet run -- init my-project    # Test project creation with modules
dotnet run -- build             # Test module detection and parallel execution  
dotnet run -- watch             # Test dev server + hot reload
dotnet run -- publish           # Test production build with optimization
dotnet run -- add-page contact  # Test page generation
```

## File Locations Quick Ref
- **Entry Point**: Runner.cs → WorkflowFactory.cs
- **Workflows**: Engine/Workflows/{Init|Build|Publish|AddPage}Workflow.cs
- **Modules**: Engine/Modules/{Client|Server|Shared}Module.cs
- **Workers**: Engine/Workers/{Client|Server|Shared}/
- **Resources**: CLI/Resources/{client|server|shared}/
- **Services**: Engine/Services/{WorkflowFactory|WatchService}.cs
- **Workspaces**: CLI/out/{workflow-name}/ (isolated per workflow)