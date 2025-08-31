# Webstir Code Patterns & Conventions

## Code Style Philosophy
- **YAGNI (You Aren't Gonna Need It)** - Don't add functionality until it's actually needed
- Write self-documenting code with clear method names instead of comments
- Extract complex logic into small, focused methods (Single Responsibility Principle)
- Main methods should read like high-level outlines
- Avoid dense lambdas - extract to named methods for clarity
- Only write comments for WHY, not WHAT (code should explain what)
- Prefer foreach over complex LINQ when it improves readability
- Group related functionality using SOLID principles
- Use try/catch sparingly - only at boundaries or when adding context
- Let exceptions bubble up - don't catch just to log and rethrow
- Omit braces for single-line if statements and loops
- Apply all IDE diagnostic suggestions to keep code clean and optimized
- Keep spacing tight and consistent - similar code blocks should look uniform
- Avoid excessive blank lines that break visual flow
- Always use explicit types - no var (except anonymous types)
- Use target-typed new() to avoid redundancy

## YAGNI Examples
- Don't create abstractions for "future flexibility" - wait until you have 2+ real use cases
- Don't add optional parameters that aren't being used yet
- Don't create base classes until you have multiple implementations
- Keep constants/helpers close to where they're used until needed elsewhere
- Example: ModuleConstants stays in ModuleGraph namespace until other code needs it

## Method Design
- **Single vs Multiple: Always handle both with one method**
- Don't create separate methods for single item vs collection
- A single item is just a collection of one
- BAD: `ProcessFile(string file)` AND `ProcessFiles(string[] files)` 
- GOOD: `ProcessFiles(params string[] files)` - handles both cases
- BAD: Special logic for count == 1 unless there's a performance reason
- GOOD: Let the same loop handle 1 or N items uniformly

## Spacing & Visual Consistency
- Keep related operations together without blank lines
- Consistent patterns should look the same (e.g., similar lambdas)
- Use blank lines only between distinct logical groups
- No random gaps that break visual flow
- Code should "look pretty" - uniform and cohesive
- **Always add a blank line before the final return statement of a method**

## Diagnostic Optimizations
Always apply these common diagnostic suggestions:
- Use `GeneratedRegex` for compile-time regex generation
- Use `string.StartsWith(char)` instead of `string.StartsWith(string)` for single chars
- Use `Count == 0` instead of `Any()` for performance
- Cache `JsonSerializerOptions` instances instead of creating new ones
- Use collection expressions `[..]` instead of `.ToArray()`
- Mark classes as `partial` when using source generators
- Remove unused parameters or use discard `_`
- Simplify collection initialization where possible

## Type Declaration Pattern
```csharp
// GOOD - Explicit types, readable without IDE
int count = items.Count;
string name = user.Name;
List<string> names = new();
Dictionary<string, User> userMap = new();

// BAD - Requires hovering to understand
var count = items.Count;
var name = user.Name;
var names = new List<string>();

// Exception: Anonymous types (no choice)
var result = new { Name = "John", Age = 30 };
```

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
// ONLY catch when you can add value:
// 1. At application boundaries (controllers, workflows)
// 2. When adding specific context before rethrowing
// 3. When you can actually handle/recover

// GOOD: Adds context about which file failed
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing {File}", fileName);
    throw; // Still bubbles up with added context
}

// BAD: Pointless catch
catch (Exception ex)
{
    throw; // Just let it bubble naturally
}

// BAD: Silent fallbacks that hide errors
try
{
    return ParseConfig(path);
}
catch 
{
    return DefaultConfig(); // Makes debugging impossible
}
```

**IMPORTANT: Fail hard, fail fast**
- No silent fallbacks - they make debugging a nightmare
- If something is wrong, let it crash with a clear error
- Better to fix the root cause than mask it with fallbacks
- Users prefer clear errors over mysterious behavior

When recovery is acceptable, prefer surfacing a diagnostic (file, line, column, clear message) over silent fallback.

## Logging
- **NO LOGGING unless explicitly requested**
- Don't add log statements speculatively
- Logging decisions should be made when the system is complete
- Premature logging clutters code and makes assumptions about what needs tracking

## Template/Resource Embedding
```csharp
// Templates from Engine/Templates/ are embedded in assembly
// During init, they're copied to the new project
await ResourceHelpers.CopyEmbeddedDirectoryAsync(
    Templates.ClientTemplatesPath,  // Engine.Templates.src.client
    workspace.ClientPath            // Project's src/client/
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

---

References:
- `.codex/Codex-Patterns.md` (this doc) for general guidance
- `Docs/framework-pipeline-codex/09-working-agreements.md` for feature‑specific agreements
