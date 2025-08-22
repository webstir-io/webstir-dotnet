# Webstir Code Patterns & Conventions

## Naming Conventions
- **Files**: PascalCase for classes (e.g., `HtmlHandler.cs`)
- **Directories**: PascalCase for code, lowercase for content
- **Constants**: Defined in `Engine/Constants.cs`
- **Folders**: Static class `Folders` for directory names
- **Files**: Static class `Files` for file names

## Dependency Injection Pattern
```csharp
// Primary constructor injection (C# 12)
public class HtmlHandler(AppWorkspace workspace, ILogger<HtmlHandler> logger)
{
    // No need for private fields, parameters are available throughout
}
```

## Workflow Pattern
```csharp
public class MyWorkflow : BaseWorkflow
{
    public override string WorkflowName => Commands.MyCommand;
    
    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        // Use ExecuteWorkersAsync for parallel execution
        await ExecuteWorkersAsync(async worker => await worker.BuildAsync());
    }
}
```

## Worker Pattern
```csharp
public class MyWorker : IWorker
{
    public int BuildOrder => 1; // Controls execution order
    
    public async Task BuildAsync(string? changedFilePath = null)
    {
        // Skip if change not relevant
        if (!BuildHelpers.ContainsBuildFolder(changedFilePath, Folders.Client))
            return;
            
        // Process files
    }
}
```

## Handler Pattern
```csharp
public class MyHandler(AppWorkspace workspace)
{
    public async Task BuildAsync() { /* Development build */ }
    public async Task PublishAsync() { /* Production build */ }
    public async Task AddPageAsync(string pageName) { /* Scaffold */ }
}
```

## Path Management
```csharp
// Use extension methods for path operations
string path = workspace.ClientPath.Combine("file.ts");
string subdir = workspace.ClientPath.CreateSubDirectory("pages");
bool exists = path.Exists();
string name = path.Name();
```

## File Processing Pattern
```csharp
// Process all files of a type
foreach (var file in directory.Files("*.css"))
{
    // Process file
}

// Parallel processing
await Task.WhenAll(
    handler1.BuildAsync(),
    handler2.BuildAsync()
);
```

## Error Handling
```csharp
// Log and rethrow for visibility
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing {File}", fileName);
    throw; // Let workflow handle
}
```

## Resource Embedding
```csharp
// Templates embedded in assembly
await ResourceHelpers.CopyEmbeddedDirectoryAsync(
    Resources.ClientResourcesPath, 
    workspace.ClientPath
);
```

## Process Execution
```csharp
// Run external processes
await ProcessRunner.RunAsync("npx", "tsc -p tsconfig.json");
```

## Configuration Constants
```csharp
// All strings centralized
public static class Folders
{
    public const string Src = "src";
    public const string Client = "client";
    public const string Server = "server";
}
```

## Async/Await Patterns
- Always use `async/await` for I/O operations
- Use `Task.WhenAll` for parallel operations
- Return `Task.CompletedTask` for sync operations in async methods

## Logging Pattern
```csharp
// Structured logging with Serilog
_logger.LogInformation("Building {PageCount} pages", pageCount);
_logger.LogError(ex, "Failed to process {File}", fileName);
```

## File Change Detection
```csharp
// Debounce duplicate events
private readonly Dictionary<string, List<DateTime>> _pendingEvents = new();
// Check timestamp to avoid duplicates
```

## Project Mode Branching
```csharp
// Adapt behavior based on mode
var mode = Context.DetectProjectMode();
var workers = mode switch
{
    ProjectMode.ClientOnly => [clientWorker],
    ProjectMode.ServerOnly => [serverWorker],
    _ => [clientWorker, serverWorker, sharedWorker]
};
```