# Fullstack Evolution Plan for Webstir

## Overview
Evolution of webstir from a static site generator to a fullstack TypeScript framework using a proxy-based approach that preserves the existing development workflow.

## Architecture Changes

### Directory Structure
```
src/
├── client/       # All client-side code (moved from src/)
│   ├── app/      # Shared templates (app.html, app.css, app.ts)
│   ├── pages/    # Individual pages
│   └── images/   # Static assets
├── server/       # NEW: Server-side TypeScript code
│   ├── index.ts  # Server entry point
│   ├── api/      # API endpoints
│   ├── models/   # Data models
│   └── tsconfig.json
└── shared/       # NEW: Shared types between client/server
    ├── types/    # Type definitions used by both
    ├── schemas/  # API schemas, validation rules
    └── index.ts  # Main export file

build/
├── client/       # Client build output (was build/bin)
└── server/       # Server build output (compiled JS)

dist/
├── client/       # Production client files
└── server/       # Production server files
```

### Development Architecture
- **Port 8088**: Existing ASP.NET Core server (serves static files, hot reload)
- **Port 3001**: Node.js/Express API server
- **Proxy**: `/api/*` requests forwarded from 8088 → 3001

## Implementation Steps

### 1. Update Server.cs
Add simple proxy middleware for API requests:
```csharp
// Add after existing middleware in Start()
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), 
    app => app.RunProxy("http://localhost:3001"));

// Note: May need to install Microsoft.AspNetCore.Proxy or similar lightweight proxy package
```

### 2. Add Node.js Process Management
Update Runner.cs to manage the Node.js server lifecycle:
```csharp
private Process? _nodeProcess;

private async Task StartNodeServer()
{
    _nodeProcess = new Process {
        StartInfo = new ProcessStartInfo {
            FileName = "node",
            Arguments = "build/server/index.js",
            UseShellExecute = false,
            RedirectStandardOutput = true
        }
    };
    _nodeProcess.Start();
    await Task.Delay(500); // Wait for server to start
}

// Call in Watch command, stop in cleanup
```

### 3. Create ServerWorker
New worker to compile server TypeScript:
- Watches `src/server/**/*.ts`
- Runs `tsc -p src/server/tsconfig.json`
- Outputs to `build/server/`
- BuildOrder: Runs parallel with client workers

### 4. Update Existing Workers
Modify paths to use `src/client/` instead of `src/`:
- MarkupWorker
- StylesWorker
- ScriptsWorker
- ImagesWorker

### 5. Create Initial Server
```typescript
// src/server/index.ts
import { createServer, IncomingMessage, ServerResponse } from 'http';
import { URL } from 'url';

const server = createServer((req: IncomingMessage, res: ServerResponse) => {
  const url = new URL(req.url!, `http://${req.headers.host}`);
  
  // Set CORS headers for development
  res.setHeader('Access-Control-Allow-Origin', 'http://localhost:8088');
  res.setHeader('Content-Type', 'application/json');
  
  if (req.method === 'GET' && url.pathname === '/api/health') {
    res.writeHead(200);
    res.end(JSON.stringify({ status: 'ok', timestamp: Date.now() }));
  } else {
    res.writeHead(404);
    res.end(JSON.stringify({ error: 'Not found' }));
  }
});

server.listen(3001, () => {
  console.log('API server running on port 3001');
});
```

```json
// src/server/tsconfig.json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "commonjs",
    "outDir": "../../build/server",
    "rootDir": ".",
    "esModuleInterop": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true
  }
}
```

## Build Strategy Improvements

### Current vs. New Build Approach
**Current**: Clean build directory on every change
- Pros: No stale files, predictable
- Cons: Slower rebuilds, loses TypeScript incremental compilation

**New**: Incremental builds by default
- Use TypeScript's `--incremental` flag with `.tsbuildinfo` files
- Only clean build when necessary (config changes, deleted files)
- Selective rebuilds based on changed files

### Build Logic Updates
```csharp
// In Runner.cs Build()
public void Build(bool releaseMode = false, bool cleanBuild = false)
{
    if (cleanBuild || ShouldCleanBuild())
    {
        // Current clean logic
        foreach (var directory in Directories.BuildDirectory.GetDirectories())
            directory.Delete(true);
    }
    
    // Incremental build by default
    foreach (var worker in _webFileWorkers.OrderBy(p => p.BuildOrder))
        worker.Build(releaseMode);
}

private bool ShouldCleanBuild()
{
    // Clean if tsconfig files changed, or files were deleted
    return HasConfigChanged() || HasFilesBeenDeleted();
}
```

### Worker Intelligence
- **Client changes**: Only rebuild client
- **Server changes**: Only rebuild server + restart Node.js
- **Shared changes**: Rebuild both client and server
- **Config changes**: Force clean build

### TypeScript Compilation Updates
```json
// Add to tsconfig files
{
  "compilerOptions": {
    "incremental": true,
    "tsBuildInfoFile": "../../build/.tsbuildinfo-client" // or -server
  }
}
```

## Development Workflow
1. `dotnet run -- watch` starts both:
   - ASP.NET Core server (port 8088)
   - Node.js API server (port 3001)
2. File changes trigger intelligent rebuilds:
   - Client changes → incremental client rebuild → browser refresh
   - Server changes → incremental server rebuild → Node.js restart
   - Shared changes → rebuild both → restart server + refresh browser
3. API calls to `/api/*` automatically proxied
4. Optional: `dotnet run -- build --clean` for full clean build

## Benefits of This Approach
- **Minimal disruption**: Existing workflow remains intact
- **Incremental adoption**: Add backend features as needed
- **Clear separation**: Client and server code organized separately
- **Familiar tools**: Leverage existing TypeScript compilation
- **Easy rollback**: Can remove server components without breaking client

## Flexible Mode Detection

Webstir should automatically detect the project structure and operate accordingly:

### Detection Logic
```csharp
// In Runner.cs
private ProjectMode DetectProjectMode()
{
    bool hasClient = Directory.Exists("src/client");
    bool hasServer = Directory.Exists("src/server");
    bool hasLegacy = Directory.Exists("src/app") || Directory.Exists("src/pages");
    
    if (hasClient && hasServer) return ProjectMode.Fullstack;
    if (hasClient && !hasServer) return ProjectMode.ClientOnly;
    if (!hasClient && hasServer) return ProjectMode.ServerOnly;
    if (hasLegacy) return ProjectMode.Legacy;
    
    return ProjectMode.Unknown;
}
```

### Mode Behaviors

**Legacy Mode** (current structure):
- `src/app`, `src/pages`, `src/images`
- Works exactly as it does today
- No Node.js process, no proxy

**Client-Only Mode**:
- Only `src/client/` exists
- Behaves like legacy but with new paths
- No Node.js process, no proxy

**Server-Only Mode**:
- Only `src/server/` exists
- No static file serving
- Just compiles TypeScript and runs Node.js
- Could be used for APIs, CLI tools, etc.

**Fullstack Mode**:
- Both `src/client/` and `src/server/` exist
- Full proxy setup as described above

### Benefits
- **Backwards compatible**: Existing projects continue to work
- **Flexible**: Use webstir for any TypeScript project
- **Progressive**: Start with client-only, add server later
- **Smart defaults**: No configuration needed

## TypeScript Configuration

### Base Configuration
Use a shared base config for consistent settings:

```json
// tsconfig.base.json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "esnext",
    "moduleResolution": "node",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "sourceMap": true,              // Enable source maps for debugging
    "declaration": false,
    "removeComments": true
  }
}
```

### Client Configuration
```json
// src/client/tsconfig.json
{
  "extends": "../../tsconfig.base.json",
  "compilerOptions": {
    "target": "ES2020",             // Browser compatibility
    "lib": ["ES2020", "DOM"],
    "outDir": "../../build/client",
    "paths": {
      "@shared/*": ["../shared/*"]
    }
  },
  "include": [".", "../shared"]
}
```

### Server Configuration
```json
// src/server/tsconfig.json
{
  "extends": "../../tsconfig.base.json",
  "compilerOptions": {
    "target": "ES2022",             // Node.js supports newer features
    "module": "commonjs",           // Node.js module system
    "lib": ["ES2022"],
    "outDir": "../../build/server",
    "paths": {
      "@shared/*": ["../shared/*"]
    }
  },
  "include": [".", "../shared"]
}
```

## Shared Types Implementation

### Example Usage
```typescript
// src/shared/types/user.ts
export interface User {
  id: string;
  email: string;
  name: string;
}

// src/server/api/users.ts
import { User } from '@shared/types/user';

app.get('/api/users/:id', async (req, res) => {
  const user: User = await getUser(req.params.id);
  res.json(user);
});

// src/client/pages/profile/index.ts
import { User } from '@shared/types/user';

async function loadProfile(): Promise<User> {
  const response = await fetch('/api/users/me');
  return response.json();
}
```

### Benefits
- **Type safety**: API contracts enforced on both sides
- **Single source of truth**: Change types once, both sides update
- **Better refactoring**: TypeScript catches breaking changes
- **API documentation**: Types serve as documentation

## Production Build Updates

### Publish Command Behavior
The `dotnet run -- publish` command adapts based on detected project mode:

#### Legacy/Client-Only Mode
- Current behavior unchanged
- Compiles TypeScript (no source maps)
- Outputs to `dist/` or `dist/client/`
- Removes development scripts (refresh.js)

#### Server-Only Mode
- Compiles server TypeScript (no source maps)
- Outputs to `dist/server/`
- Copies `package.json` for dependencies
- Creates production-ready Node.js app

#### Fullstack Mode
```
dist/
├── client/           # Static files ready for any web server
│   ├── app.js       
│   ├── app.css
│   └── pages/
├── server/          # Node.js application
│   ├── index.js
│   └── api/
└── package.json     # Server dependencies only
```

### Key Publishing Changes
- **No source maps** in production builds
- **Separate outputs** for client and server
- **package.json** includes only production dependencies
- **Clean separation** allows flexible deployment options

Users can then:
- Serve `dist/client/` from any static host (CDN, nginx, etc.)
- Deploy `dist/server/` to any Node.js host
- Or combine them in their preferred configuration

## Future Enhancements
- Implement API route generation from TypeScript types
- Add database integration
- Auto-generate client SDK from server routes
- Bundle optimization options