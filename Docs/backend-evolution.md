# Webstir Full-Stack Framework Plan

## Vision
Transform Webstir from a static site generator into a minimalist full-stack web framework for C#/.NET, maintaining its core philosophy of simplicity, minimal dependencies, and file-based conventions.

## Core Principles
1. **Zero mandatory dependencies** - Core framework requires only .NET runtime
2. **Bring your own libraries** - Users choose their database, auth, etc.
3. **File-based routing** - Intuitive project structure drives behavior
4. **Plain C# handlers** - No decorators, minimal magic
5. **Progressive enhancement** - Start static, add dynamic features as needed
6. **Single binary deployment** - Self-contained executable

## Architecture Overview

### Directory Structure
```
src/
├── app/              # Application-wide templates and assets
│   ├── app.html      # Master template
│   ├── app.css       # Global styles
│   └── app.ts        # Global client scripts
├── pages/            # File-based page routing
│   └── about/
│       ├── index.html
│       ├── index.css
│       ├── index.ts
│       └── index.cs  # NEW: Server-side page handler
├── api/              # NEW: API endpoints
│   └── users/
│       └── index.cs  # Handles /api/users
├── middleware/       # NEW: Request pipeline
│   └── auth.cs       # Custom middleware
├── lib/              # NEW: Shared server code
└── images/           # Static assets
```

### Core Components

#### 1. Server-Side Pages
Each page can optionally have an `index.cs` handler:
```csharp
// src/pages/about/index.cs
public class AboutPage : PageHandler
{
    public override async Task<PageData> OnGet(HttpContext context)
    {
        return new PageData
        {
            Title = "About Us",
            Props = new { CompanyName = "Acme Corp" }
        };
    }
}
```

#### 2. API Endpoints
Simple HTTP handlers in the `api/` directory:
```csharp
// src/api/users/index.cs
public class UsersApi : ApiHandler
{
    public override async Task<object> OnGet(HttpContext context)
    {
        // Return JSON automatically serialized
        return new { users = new[] { "Alice", "Bob" } };
    }
    
    public override async Task<object> OnPost(HttpContext context)
    {
        var body = await context.ReadJsonAsync<CreateUserRequest>();
        return new { id = 123, name = body.Name };
    }
}
```

#### 3. Middleware System
```csharp
// src/middleware/auth.cs
public class AuthMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, Next next)
    {
        if (context.Path.StartsWith("/admin") && !IsAuthenticated(context))
        {
            context.Response.StatusCode = 401;
            return;
        }
        await next();
    }
}
```

#### 4. Template Engine Enhancement
Extend existing HTML merging to support server-side data:
```html
<!-- src/pages/products/index.html -->
<div class="products">
    {{#each products}}
    <div class="product">
        <h2>{{name}}</h2>
        <p>${{price}}</p>
    </div>
    {{/each}}
</div>
```

### Development Workflow

#### Commands
```bash
# Development (with hot reload)
webstir dev         # Runs full server with file watching

# Production
webstir build       # Compiles everything
webstir start       # Runs production server

# Deployment
webstir publish     # Creates self-contained binary
```

#### Development Server Features
- Existing hot reload for client-side changes
- Auto-recompilation of C# handlers
- Request logging and error pages
- Mock data support

### Implementation Phases

#### Phase 1: Core Server Runtime
- Extend Server.cs to handle dynamic requests
- Basic request routing based on file structure
- JSON serialization for API responses
- Static file serving alongside dynamic routes

#### Phase 2: Page Handlers
- PageHandler base class
- Server-side data injection into templates
- Simple template syntax (mustache-like)
- Build-time template precompilation

#### Phase 3: API Layer
- ApiHandler base class
- HTTP method routing
- Request body parsing
- Response helpers

#### Phase 4: Middleware
- Middleware pipeline
- Built-in middleware (cors, compression)
- Custom middleware support
- Per-route middleware

#### Phase 5: Production Features
- Production server mode
- Environment configuration
- Logging infrastructure
- Health checks

### Data Layer Philosophy
No built-in ORM or database. Instead, provide helpers:
```csharp
// User brings their own data access
public class UsersApi : ApiHandler
{
    private readonly IDbConnection _db;
    
    public UsersApi()
    {
        // User configures their own connection
        _db = new SqliteConnection("...");
    }
}
```

### Deployment Story
```bash
# Single binary with embedded assets
webstir publish --self-contained --runtime linux-x64

# Outputs: ./dist/myapp (single executable)
# Deploy anywhere, run with: ./myapp

# Or traditional .NET deployment
webstir publish
# Outputs: ./dist/ (with dll files)
# Run with: dotnet myapp.dll
```

### Differentiators
1. **True file-based routing** - No route configuration needed
2. **Minimal abstractions** - Easy to understand and debug
3. **Static-first** - Pages work without JavaScript
4. **No framework lock-in** - Standard C# code throughout
5. **Single tool** - Build, dev server, and production runtime

### Example App Structure
```
blog/
├── src/
│   ├── app/
│   │   └── app.html         # Blog layout
│   ├── pages/
│   │   ├── index.cs         # Home page handler
│   │   ├── index.html       # Home template
│   │   └── posts/
│   │       ├── [slug]/      # Dynamic route
│   │       │   ├── index.cs # Post detail handler
│   │       │   └── index.html
│   │       └── index.cs     # Posts list handler
│   ├── api/
│   │   └── posts/
│   │       └── index.cs     # Posts CRUD API
│   └── lib/
│       └── database.cs      # Shared DB code
└── webstir.json            # Configuration
```

### Open Questions
1. Template syntax - Mustache? Liquid? Custom?
2. Configuration approach - JSON? Code-based?
3. Plugin/extension system?
4. Built-in auth helpers or fully BYO?
5. WebSocket support?
6. Background job system?

### Next Steps
1. Prototype the server runtime extensions
2. Implement basic page handlers
3. Create example applications
4. Gather feedback on API design
5. Document migration path from static to full-stack