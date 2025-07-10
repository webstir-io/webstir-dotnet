# webstir

A minimalist fullstack framework written in C# (.NET 9.0) that combines static site generation with Node.js backend integration.

## Features

- **Fullstack Development**: Seamlessly integrate frontend and backend development
- **TypeScript Support**: Built-in TypeScript compilation for both frontend and backend
- **Hot Reload**: Development server with WebSocket-based hot reload
- **API Proxy**: Automatic proxying between frontend and backend during development
- **Node.js Integration**: Run Node.js servers alongside your static site
- **Page-Based Architecture**: Organized page structure with HTML, CSS, and TypeScript
- **Client-Side Routing**: Optional SPA routing with lifecycle hooks and navigation API
- **Template System**: HTML fragments merged with app-level templates
- **Production Ready**: Optimized builds for deployment
- **Built-in Help System**: Comprehensive help for all commands
- **Demo Command**: Instant demo app showcasing all features with one command

## Installation

```bash
# Clone the repository
git clone https://github.com/yourusername/webstir.git
cd webstir

# Build the CLI tool
dotnet build

# Or create a self-contained executable
./publish.sh
```

## Quick Start

```bash
# See webstir in action with a demo app
dotnet run -- demo

# Or create your own project
dotnet run -- init
dotnet run -- add-page home

# Start development server
dotnet run --
```

## Commands

### Getting Help
```bash
# Show all available commands
dotnet run -- help

# Show help for a specific command
dotnet run -- help init
dotnet run -- init --help

# Quick help
dotnet run -- --help
dotnet run -- -h
```

### Development
```bash
# Create a demo application showcasing all webstir features
dotnet run -- demo              # Creates in 'demo' folder
dotnet run -- demo my-app       # Creates in 'my-app' folder

# Initialize a new project (defaults to fullstack)
dotnet run -- init

# Initialize specific project types
dotnet run -- init --client-only    # Frontend only (no Node.js backend)
dotnet run -- init --server-only    # Backend only (Node.js API server)

# Add a new page
dotnet run -- add-page <page-name>

# Build and run the fullstack development server (default command)
dotnet run -- watch
dotnet run --        # Same as watch

# Build the project once
dotnet run -- build
dotnet run -- build --clean    # Clean build (removes build directory first)

# Create production build in dist directory
dotnet run -- publish
```

## Command Reference

| Command | Description | Options |
|---------|-------------|---------|
| `help` | Show help information | `[command]` - Show help for specific command |
| `demo` | Create a demo application showcasing all webstir features | `[directory]` - Target directory (default: 'demo') |
| `init` | Initialize a new webstir project | `--client-only` - Frontend only<br>`--server-only` - Backend only |
| `add-page` | Add a new page to your project | `<page-name>` - Name of the page (required) |
| `build` | Build the project once | `--clean` - Clean build directory first |
| `watch` | Build and watch for changes (default) | None |
| `publish` | Create production build | None |

## Project Types

Webstir supports three project configurations:

### Fullstack (Default)
Complete frontend and backend setup with shared types:
- Frontend static site with TypeScript
- Node.js backend server  
- Shared types between frontend and backend
- API proxy for seamless communication

### Client-Only
Frontend-only static site:
- Perfect for static websites, SPAs, or JAMstack sites
- No backend server or API proxy
- Lighter weight deployment

### Server-Only  
Backend API server only:
- Node.js/Express API server
- No frontend files
- Ideal for microservices or headless APIs

## Project Structure

### Fullstack Project Structure
```
src/
├── client/           # Frontend code
│   ├── app/          # Shared template files
│   │   ├── app.html  # Main HTML template
│   │   ├── app.css   # Global styles
│   │   └── app.ts    # Global TypeScript
│   ├── pages/        # Frontend pages
│   │   └── home/     # Example page
│   │       ├── index.html  # Page content (fragment)
│   │       ├── index.css   # Page-specific styles
│   │       └── index.ts    # Page-specific TypeScript
│   ├── images/       # Image assets
│   └── tsconfig.json # Frontend TypeScript config
├── server/           # Backend Node.js code
│   ├── index.ts      # Server entry point
│   └── tsconfig.json # Backend TypeScript config
└── shared/           # Shared code between client and server
    └── types/        # Shared TypeScript types/interfaces
        └── index.ts  # Type definitions

build/                # Development builds
├── client/           # Compiled frontend code
│   ├── app/          # Compiled app files
│   ├── pages/        # Compiled page files
│   ├── images/       # Copied images
│   ├── index.html    # Generated HTML
│   └── refresh.js    # Hot reload script
├── server/           # Compiled backend TypeScript
│   └── index.js      # Compiled server entry point
└── shared/           # Compiled shared code
    └── types/        # Compiled type definitions

dist/                 # Production builds
```

## How It Works

### Frontend
1. **Pages**: Each page is a directory in `src/client/pages/` containing HTML, CSS, and TypeScript
2. **Templates**: Page HTML fragments are merged into `src/client/app/app.html` at build time
3. **Styles**: CSS files are concatenated (app.css + page-specific CSS)
4. **Scripts**: TypeScript is compiled to ES modules with separate tsconfig

### Backend
1. **Node.js Server**: Automatically managed process that runs your backend code
2. **API Proxy**: Frontend requests to `/api/*` are automatically proxied to your Node.js backend
3. **TypeScript**: Backend code is compiled and watched for changes
4. **Hot Reload**: Server restarts automatically on backend changes
5. **Process Management**: The framework handles starting, stopping, and restarting the Node.js process

### Shared Types
Webstir supports sharing TypeScript types between frontend and backend:

```typescript
// src/shared/types/index.ts
export interface User {
  id: string;
  name: string;
  email: string;
}

// Frontend (src/client/pages/users/index.ts)
import { User } from '../../../shared/types';
const users: User[] = await fetch('/api/users').then(r => r.json());

// Backend (src/server/index.ts)
import { User } from '../shared/types';
app.get('/api/users', (req, res) => {
  const users: User[] = getUsersFromDB();
  res.json(users);
});
```

### Client-Side Routing

Webstir includes an optional client-side routing system for building single-page applications (SPAs). The router automatically detects pages that export route handlers and enables SPA navigation between them.

#### Basic Usage

Create a route handler in your page's TypeScript file:

```typescript
// src/client/pages/products/index.ts
import type { RouteHandler } from '@shared/router-types';

export const routeHandler: RouteHandler = {
  onEnter: async (params) => {
    console.log('Entering products page', params);
    // Load products, update UI, etc.
  },
  
  onLeave: async () => {
    console.log('Leaving products page');
    // Cleanup, save state, etc.
  }
};
```

#### Navigation API

Use the navigation API to programmatically navigate between pages:

```typescript
import { navigate } from '@webstir/navigation';

// Navigate to another page
await navigate('/products/');

// Navigate with query parameters
await navigate('/products/?category=electronics');
```

#### Route Lifecycle

- **onEnter**: Called when navigating to a page (receives query parameters)
- **onLeave**: Called when navigating away from a page
- **onUpdate**: Called when query parameters change on the same route

#### How It Works

1. **Automatic Detection**: The build system detects pages that export `routeHandler`
2. **Metadata Injection**: Routing metadata is injected into the HTML during development
3. **Dynamic Loading**: The router is only loaded for projects with SPA pages
4. **Link Interception**: Internal links are automatically intercepted for SPA navigation
5. **Fallback Support**: Non-SPA pages continue to work with traditional navigation

#### Example: Multi-Page SPA

```typescript
// src/client/pages/products/index.ts
export const routeHandler: RouteHandler = {
  onEnter: async (params) => {
    const category = params.category || 'all';
    await loadProducts(category);
  }
};

// src/client/pages/product-detail/index.ts
export const routeHandler: RouteHandler = {
  onEnter: async (params) => {
    if (params.id) {
      await loadProductDetail(params.id);
    }
  }
};

// Navigation between pages
import { navigate } from '@webstir/navigation';

// From products page to detail page
await navigate('/product-detail/?id=123');
```

### Development Server

The development server includes:
- Static file serving on `http://localhost:8088`
- Node.js backend server on port 3001
- API proxy for seamless frontend-backend communication
- WebSocket server on port 3456 for hot reload
- Automatic server restart on backend changes

## Configuration

Configure your project in `webstir.json`:

```json
{
  "ApiBaseUrl": "http://localhost:3001",
  "NodeServerPath": "./build/server/index.js",
  "ServerPort": 3001
}
```

## Creating Projects

### Fullstack App (Default)
```bash
# Initialize a new fullstack project
dotnet run -- init

# Add a new page
dotnet run -- add-page dashboard

# Start the fullstack development server
dotnet run -- watch
```

### Client-Only App
```bash
# Initialize a frontend-only project
dotnet run -- init --client-only

# Add pages as needed
dotnet run -- add-page about
dotnet run -- add-page contact

# Run the static development server
dotnet run -- watch
```

### Server-Only API
```bash
# Initialize a backend-only project
dotnet run -- init --server-only

# Start the Node.js development server
dotnet run -- watch
```

Your frontend can now make requests to `/api/*` which will be proxied to your Node.js backend:

```typescript
// Frontend (src/client/pages/dashboard/index.ts)
const response = await fetch('/api/users');
const users = await response.json();

// Backend (src/server/index.ts)
app.get('/api/users', (req, res) => {
  res.json({ users: [...] });
});
```

## Building for Production

```bash
# Create optimized build in dist/
dotnet run -- publish
```

Production builds:
- Compile all TypeScript (frontend and backend)
- Remove development scripts
- Optimize static assets
- Ready for deployment

## Requirements

- .NET 9.0 SDK
- Node.js 18+ and npm
- TypeScript (`npm install -g typescript`)

## Why webstir?

Webstir bridges the gap between static site generators and fullstack frameworks. It's perfect for projects that need both static content and dynamic API functionality without the complexity of larger frameworks.

## License

MIT