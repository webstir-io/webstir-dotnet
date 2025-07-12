# CLAUDE.md - Implementation Quick Reference

## Critical Constants
- **Ports**: WebServer: 8088, Node API: 3001, WebSocket: 3456
- **Build Paths**: Frontend: `build/client/`, Backend: `build/server/`, Dist: `dist/`
- **Entry Points**: Runner.cs → Workers → Build/Watch/Publish

## Recent Features (July 2025)

### CSS @import Support ✨ NEW
- **Detection**: StylesWorker checks for @import in app.css
- **Build**: Preserves imports, copies files, rewrites paths
- **Publish**: Recursively inlines all imports
- **Namespaces**: @app/ → app/, @components/ → app/components/
- **Location**: StylesWorker.cs:195-309

### Client-Side Routing
- **Detection**: MarkupWorker scans for `export const routeHandler`
- **Metadata**: Injected as JSON in dev mode only
- **Location**: MarkupWorker.cs:156-195, router.ts, navigation.ts

### Demo Command
- **Auto-cleanup**: Deletes existing demo folder
- **Pattern**: ITemplateBuilder → DemoBuilder → EmbeddedResources
- **Location**: DemoBuilder.cs, Runner.cs:ExecuteDemo()

## Architecture Patterns

### Workers (BuildOrder 1-5)
1. **ScriptsWorker**: Compiles TypeScript, skips refresh.js in publish
2. **SharedWorker**: Handles shared types
3. **StylesWorker**: CSS processing with @import support
4. **MarkupWorker**: HTML merging, routing metadata injection
5. **ServerWorker**: Node.js compilation and process management

### Key Interfaces
- **IFileWorker**: Base for all workers (Init, Build, Publish)
- **IPageWorker**: Extends IFileWorker with AddPage
- **ITemplateBuilder**: For demo/template creation

## Common Pitfalls
- **Path.Combine**: Use for all paths, never hardcode
- **Settings.IsProduction**: Check before injecting dev scripts
- **Process Disposal**: NodeService must dispose to kill process
- **Resource Paths**: Must match `CLI.Resources.{path}.{file}` pattern

## Testing Checklist
```bash
dotnet run -- build          # Test compilation
dotnet run -- watch          # Test dev server + hot reload
dotnet run -- publish        # Test production build
dotnet run -- demo test      # Test demo creation
```

## File Locations Quick Ref
- **Commands**: Runner.cs (private methods), Helper.cs (help text)
- **Constants**: Commands.cs (all string literals)
- **Workers**: CLI/Workers/{Client|Server|Shared}/
- **Resources**: CLI/Resources/{client|server|shared}/
- **Services**: WebServer.cs (Kestrel), NodeService.cs (Node process)