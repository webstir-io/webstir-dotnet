# webstir

A minimalist fullstack framework written in C# (.NET 9.0) that combines static site generation with Node.js backend integration.

## Features

- **Fullstack Development**: Seamlessly integrate frontend and backend development
- **TypeScript Support**: Built-in TypeScript compilation for both frontend and backend
- **Hot Reload**: Development server with WebSocket-based hot reload
- **API Proxy**: Automatic proxying between frontend and backend during development
- **Node.js Integration**: Run Node.js servers alongside your static site
- **Page-Based Architecture**: Organized page structure with HTML, CSS, and TypeScript
- **Template System**: HTML fragments merged with app-level templates
- **Production Ready**: Optimized builds for deployment

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

## Commands

### Development
```bash
# Initialize a new project (defaults to fullstack)
dotnet run -- init

# Initialize specific project types
dotnet run -- init --client-only    # Frontend only (no Node.js backend)
dotnet run -- init --server-only    # Backend only (Node.js API server)

# Add a new page
dotnet run -- add-page <page-name>

# Build and run the fullstack development server
dotnet run -- watch

# Build the project to build/bin directory
dotnet run -- build

# Create production build in dist directory
dotnet run -- publish
```

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