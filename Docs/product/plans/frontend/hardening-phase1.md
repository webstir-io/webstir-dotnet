# Frontend Pipeline Hardening - Phase 1 Implementation

## Goal
Standardize error handling across all frontend pipelines for consistent, clear failure reporting.

## Current State Problems
- **Mixed error patterns**: Some pipelines throw exceptions, others use DiagnosticCollection, others use ILogger
- **Unclear errors**: Missing file paths and context in error messages
- **Inconsistent behavior**: HTML pipeline throws on missing fragments, others silently continue
- **No clear failure signal**: Workers don't know when pipelines fail

## Implementation Strategy
Use ILogger for error reporting (already injected everywhere) and return bool from pipeline methods to indicate success/failure.

## Implementation Tasks

### 1. Update Pipeline Interfaces
Change all IPageHandler and IFrontendHandler methods to return success/failure:
```csharp
Task<bool> BuildAsync(string? changedFilePath = null);
Task<bool> PublishAsync();
```
Return `false` if any errors occurred, `true` if successful.

**Note**: Also update `AddPageAsync` in IPageHandler to return `Task<bool>`.

### 2. Convert HTML Pipeline
**Files**: `Engine/Pipelines/Html/HtmlHandler.cs`, `HtmlBuilder.cs`

Current issues:
- Throws exceptions at lines 39, 63 in HtmlBuilder
- Uses DiagnosticCollection but still throws on errors
- Doesn't always include file path in errors

Changes:
- Remove all `throw new InvalidOperationException()` calls
- Replace DiagnosticCollection with direct `_logger.LogError()` calls
- Return `false` when errors occur, `true` on success
- Ensure all error messages include file path context

### 3. Update JavaScript Pipeline
**File**: `Engine/Pipelines/JavaScript/JsHandler.cs`

Current issues:
- TypeScript compilation errors thrown as exceptions (line 114)
- Process failures in RunProcess method throw exceptions (line 149)
- No DiagnosticCollection usage (good!)

Changes:
- Wrap TypeScript compilation in try-catch, log errors with `_logger.LogError()`
- Return `false` on compilation failure, `true` on success
- Parse TypeScript error output to extract file:line:column and message
- Format: `[JS] Error in file.ts:45:12 - TS2304: Cannot find name 'foo'`

### 4. Update CSS Pipeline
**Files**: `Engine/Pipelines/Css/CssHandler.cs`, `CssEsbuildAdapter.cs`

Current state:
- Creates DiagnosticCollection in BuildAsync (line 23)
- CssEsbuildAdapter throws InvalidOperationException (line 64)
- Has LogSummary method for diagnostics

Changes:
- Remove DiagnosticCollection usage entirely
- Replace LogSummary with direct `_logger.LogError()` calls
- Catch exceptions from CssEsbuildAdapter and log them
- Return `false` when bundling fails, `true` on success

### 5. Update Asset Pipelines
**Files**: `ImagesHandler.cs`, `FontsHandler.cs`, `MediaHandler.cs`

Current state:
- No error handling

Changes:
- Add try-catch around file operations
- Log errors with file paths using `_logger.LogError()`
- Return `false` on failure, `true` on success
- Log warning if source directory doesn't exist (but return `true`)

### 6. Remove DiagnosticCollection
**Files**: All files using `DiagnosticCollection`

Changes:
- Delete `DiagnosticCollection.cs` and `Diagnostic.cs`
- Remove all DiagnosticCollection parameters from method signatures
- Remove all diagnostic-related imports

### 7. Standardize Error Format
Create consistent error message format across all pipelines:

```
[Pipeline] Error in path/to/file:line - Specific error message
[HTML] Error in pages/index/index.html:15 - Missing closing tag for <div>
[CSS] Error in app/app.css:23 - Invalid property value
[JS] Error in pages/home/index.ts:45 - Cannot find module './missing'
```

### 8. Update Workers
**Files**: `FrontendWorker.cs`, `BackendWorker.cs`, `SharedWorker.cs`

Current state:
- FrontendWorker already groups handlers by BuildOrder and runs in parallel within groups
- No error checking - continues even if handlers fail

Changes:
- Track success/failure from each pipeline handler
- Run all handlers in a group to collect all errors (don't short-circuit)
- Stop processing subsequent groups if current group has failures
- Example:
  ```csharp
  bool overallSuccess = true;

  foreach (IGrouping<int, IFrontendHandler> group in _frontendHandlers
               .GroupBy(h => h.BuildOrder)
               .OrderBy(g => g.Key))
  {
      // Run all handlers in this group in parallel
      var tasks = group.Select(h => h.BuildAsync(changedFilePath)).ToList();
      var results = await Task.WhenAll(tasks);

      bool groupSuccess = results.All(r => r);
      overallSuccess &= groupSuccess;

      // Stop processing subsequent groups if this group failed
      if (!groupSuccess)
      {
          _logger.LogError("Build failed at order {Order} with errors", group.Key);
          break;
      }
  }

  if (!overallSuccess)
  {
      return; // Exit early on failure
  }
  ```

## Success Criteria
- Zero unhandled exceptions during builds
- All errors include file path and context
- Consistent error format across pipelines
- Clear true/false success signal from each pipeline
- All pipeline errors shown before build stops (not just first error)

## Testing Approach
1. Add error cases to existing test projects:
   - Remove `app.html` to test missing base template
   - Add invalid TypeScript syntax to test compilation errors
   - Create malformed CSS to test bundling errors
   - Reference missing asset files
   - Add missing HTML fragments in page directories
2. Verify each error is caught and reported clearly with standardized format
3. Ensure no exceptions bubble up to crash the build
4. Confirm all errors within a build group are shown before stopping
5. Verify TypeScript error parsing shows correct file:line:column format
6. Check that workers stop at first failing group but show all errors in that group

## Code Example

### Before (HtmlBuilder.cs)
```csharp
public async Task BuildAsync(DiagnosticCollection? diagnostics = null)
{
    if (!appHtmlPath.Exists())
    {
        _logger.LogError("Base application HTML file not found: {AppHtmlPath}", appHtmlPath);
        throw new InvalidOperationException($"Base application HTML file not found: {appHtmlPath}");
    }
    // ... rest of build
}
```

### After
```csharp
public async Task<bool> BuildAsync()
{
    if (!appHtmlPath.Exists())
    {
        _logger.LogError("[HTML] Error in {Path} - Base application HTML file not found", appHtmlPath);
        return false;
    }
    // ... rest of build
    return true;
}
```

## Rollout Plan
1. Update all interfaces to return `Task<bool>`
2. Update all pipeline implementations to log errors and return success/failure
3. Remove DiagnosticCollection and related classes
4. Update workers to check return values
5. Run full test suite to verify error handling
6. Deploy as single atomic change

## Non-Goals for Phase 1
- Performance improvements (that's Phase 2)
- New features or validations
- Changing build output or behavior
- Adding configuration options
- Implementing retry logic or recovery
- Changing the existing parallel execution within groups (keep current behavior)