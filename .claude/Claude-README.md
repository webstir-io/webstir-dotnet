# Webstir Framework - Quick Reference

## What is Webstir?
A fullstack TypeScript framework built with .NET Core that supports client-only, server-only, and fullstack applications with hot reload and intelligent builds.

## Project Structure Map
```
/CLI                    → Command-line interface
  Program.cs           → Entry point, DI setup
  Runner.cs            → Command orchestration
  
/Engine                → Core framework
  /Workflows           → Command implementations (Init, Build, Watch, Publish, AddPage)
  /Workers             → Build tasks (ClientWorker, ServerWorker, SharedWorker)
  /Handlers            → File processors (Html, Css, Scripts, Images)
  /Services            → Core services (Watch, Change, Dev, WorkflowFactory)
  /Servers             → Dev servers (WebServer, NodeServer)
  /Templates           → Project templates copied during 'init' command
  AppWorkspace.cs      → Project directory management
  
/src (user project)    → Your application code
  /client              → Frontend TypeScript
  /server              → Backend TypeScript  
  /shared              → Shared types
```

## Key Files to Know
- **Engine/Workflows/BaseWorkflow.cs** - Base class for all commands
- **Engine/AppWorkspace.cs** - Manages all project paths
- **Engine/Services/WatchService.cs** - File watching logic
- **Engine/Handlers/HtmlHandler.cs** - HTML processing and SPA detection
- **Engine/Workers/ClientWorker.cs** - Orchestrates client builds

## Quick Command Reference
```bash
dotnet run --        # Start watch mode (default)
dotnet run -- init   # Create new project
dotnet run -- build  # Compile TypeScript
dotnet run -- watch  # Dev server with hot reload
dotnet run -- publish # Production build
dotnet run -- add-page <name> # Add new page
```

## Project Modes
- **Client-Only**: Just `src/client/` - static sites, SPAs
- **Server-Only**: Just `src/server/` - APIs, services  
- **Fullstack**: Both folders - full TypeScript stack
- **Legacy**: Old structure (`src/app/`, `src/pages/`)

## Related Documentation
- `Claude-Architecture.md` - Technical architecture details
- `Claude-Commands.md` - Detailed command documentation
- `Claude-Patterns.md` - Code patterns and conventions