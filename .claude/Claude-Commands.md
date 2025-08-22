# Webstir Commands Reference

## Command Implementation Pattern
All commands follow this pattern:
1. Command → Runner.cs → WorkflowFactory → IWorkflow implementation
2. Workflow inherits BaseWorkflow for common functionality
3. Workflow orchestrates Workers → Workers use Handlers

## init
**File**: `Engine/Workflows/InitWorkflow.cs`
**Purpose**: Create new project from embedded templates
**Usage**: `dotnet run -- init [project-name] [options]`
**Options**:
- `--client-only`: Client-side only project
- `--server-only`: Server-side only project
**Process**:
1. Creates project directory
2. Copies templates from embedded resources
3. Initializes based on mode

## build
**File**: `Engine/Workflows/BuildWorkflow.cs`
**Purpose**: Compile TypeScript and process assets
**Usage**: `dotnet run -- build [options]`
**Options**:
- `--clean`: Force clean build
**Process**:
1. Detect project mode
2. Run appropriate workers (Client/Server/Shared)
3. Each worker runs its handlers (HTML, CSS, Scripts, Images)

## watch (default)
**File**: `Engine/Workflows/WatchWorkflow.cs`
**Purpose**: Development server with hot reload
**Usage**: `dotnet run -- watch` or just `dotnet run`
**Process**:
1. Start WebServer (port 8088)
2. Start NodeServer if fullstack mode (port 3001)
3. Start WatchService for file monitoring
4. Start DevService for change processing
5. Trigger rebuilds on file changes

## publish
**File**: `Engine/Workflows/PublishWorkflow.cs`
**Purpose**: Production build
**Usage**: `dotnet run -- publish`
**Process**:
1. Clean build first
2. Compile without source maps
3. Minify CSS
4. Remove dev scripts (refresh.js)
5. Output to dist/ directory

## add-page
**File**: `Engine/Workflows/AddPageWorkflow.cs`  
**Purpose**: Scaffold new page
**Usage**: `dotnet run -- add-page <page-name>`
**Creates**:
```
src/client/pages/[page-name]/
  ├── index.html
  ├── index.css
  └── index.ts
```

## help
**File**: `CLI/Help.cs`
**Usage**: 
- `dotnet run -- help` - General help
- `dotnet run -- help <command>` - Command-specific help
- `dotnet run -- <command> --help` - Alternative syntax

## Command Options Pattern
```csharp
// Engine/Commands.cs defines all options
public static class BuildOptions {
    public const string Clean = "--clean";
}
// Used in workflows like:
bool cleanBuild = args.Contains(BuildOptions.Clean);
```

## Multi-Project Support
All commands support multiple projects:
```bash
dotnet run -- build my-project
dotnet run -- build --project-name my-project
dotnet run -- build -p my-project
```
If multiple projects exist, you must specify which one.