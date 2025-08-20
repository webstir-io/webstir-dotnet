# DevService Plan

## Overview
The DevService will be a comprehensive live reload service that orchestrates all development-time services including file watching, change processing, web server management, Node.js server management, and live reload functionality.

## Current Architecture Analysis

### Existing Services
- **WatchService** (`Engine/Services/WatchService.cs:5`): Manages file system watching and delegates changes to ChangeService
- **ChangeService** (`Engine/Services/ChangeService.cs:21`): Processes file changes, manages servers, handles live reload
- **WebServer** (`Engine/Servers/WebServer.cs:14`): Serves static files and handles Server-Sent Events for live reload
- **NodeServer** (`Engine/Servers/NodeServer.cs:9`): Manages Node.js API server lifecycle

### Current Flow
1. WatchService creates FileSystemWatcher and monitors file changes
2. File changes are enqueued to ChangeService via channels
3. ChangeService processes changes and coordinates server restarts
4. WebServer handles SSE connections for live reload notifications
5. NodeServer manages the Node.js process lifecycle

## DevService Design

### Responsibilities
The DevService will act as the primary orchestrator with these responsibilities:

1. **Service Lifecycle Management**
   - Initialize and coordinate all development services
   - Graceful startup and shutdown of all services
   - Health monitoring and error recovery

2. **Live Reload Coordination**
   - Intelligent reload strategies based on file types
   - Debounced change detection
   - Client notification management

3. **Logging and Monitoring**
   - Centralized logging for all development activities
   - Error aggregation and reporting

### Architecture

```
DevService (Main Orchestrator)
├── WatchService (File System Monitoring)
├── ChangeService (Change Processing)
├── WebServer (Static File Serving + SSE)
└── NodeServer (API Server Management)
```

### Implementation Plan

#### Phase 1: Service Extraction
- Create DevService class structure
- Implement basic service lifecycle management
- Move coordination logic from ChangeService to DevService

#### Phase 2: Enhanced Monitoring
- Add health checks for all services
- Implement service restart strategies
- Add performance monitoring

#### Phase 3: Intelligent Reload
- Implement file-type specific reload strategies
- Add debouncing and batching for rapid changes
- Optimize reload performance

### Benefits

1. **Separation of Concerns**: Each service has a single responsibility
2. **Centralized Control**: Single point of coordination and monitoring
3. **Improved Reliability**: Better error handling and recovery
4. **Enhanced Performance**: Optimized reload strategies and resource management
5. **Better Developer Experience**: More responsive and predictable development environment

### Migration Strategy

1. Create DevService without breaking existing functionality
2. Gradually move coordination logic from ChangeService to DevService
3. Update CLI to use DevService as entry point
4. Remove redundant coordination code from existing services

### WatchWorkflow Integration

The current WatchWorkflow (`Engine/Workflows/WatchWorkflow.cs:6`) directly uses WatchService and handles the build coordination. With DevService, this will be simplified:

**Current Implementation:**
```csharp
public class WatchWorkflow(
    AppWorkspace context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker,
    WatchService watchService) 
    : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync();
        await watchService.Watch(Context, _ => ExecuteBuildAsync());
    }
}
```

**Proposed DevService Integration:**
```csharp
public class WatchWorkflow(
    AppWorkspace context,
    ClientWorker clientWorker,
    ServerWorker serverWorker,
    SharedWorker sharedWorker,
    DevService devService) 
    : BaseWorkflow(context, clientWorker, serverWorker, sharedWorker)
{
    protected override async Task ExecuteWorkflowAsync(string[] args)
    {
        await ExecuteBuildAsync();
        await devService.StartAsync(Context, _ => ExecuteBuildAsync());
    }
}
```

The DevService will internally coordinate all the services (WatchService, ChangeService, WebServer, NodeServer) and handle their lifecycle, making the workflow much cleaner and more maintainable.

## Next Steps

1. Create DevService class structure
2. Implement basic service lifecycle management
3. Update WatchWorkflow to use DevService
4. Add comprehensive testing
5. Update documentation and examples