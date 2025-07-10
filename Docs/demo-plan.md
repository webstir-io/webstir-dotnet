# Demo Plan

## Overview
Create a `webstir demo` command that generates a complete demo application showcasing all webstir framework features with elegant, minimalist styling.

## Goals
1. Demonstrate every webstir feature in a single cohesive app
2. Use clean, elegant styling - just essential CSS
3. Create a self-documenting example that serves as a reference
4. Make it beautiful in its simplicity

## Features to Showcase
1. **Client-Side Features**
   - Multiple pages with navigation
   - Client-side routing (SPA pages)
   - TypeScript modules and imports
   - Shared types from @shared
   - CSS styling (app-level and page-level)
   - Images and static assets
   - Hot reload in development

2. **Server-Side Features**
   - Node.js API endpoints
   - TypeScript on the server
   - API proxy integration
   - Shared types between client/server

3. **Framework Features**
   - Build system
   - Watch mode
   - Project modes (fullstack, client-only, server-only)
   - Add-page command integration

## Implementation Steps
1. ✓ Add "demo" to Commands.cs constants
2. ✓ Create Demo command in Runner.cs
3. ✓ Create DemoBuilder.cs (ITemplateBuilder pattern for future templates)
4. ✓ Define demo app structure:
   - ✓ Home page (traditional navigation)
   - ✓ About page (SPA with route handler)
   - ✓ Features page (SPA with route handler)
   - ✓ API showcase page
   - ✓ Shared types demonstration

## Demo App Structure
```
src/
  client/
    app/
      app.html
      app.css (elegant base styles)
      app.ts
    pages/
      home/
        index.html
        index.css
        home.ts
      about/
        index.html
        index.css
        about.ts (with routeHandler)
      features/
        index.html
        index.css
        features.ts (with routeHandler)
      api-demo/
        index.html
        index.css
        api-demo.ts
  server/
    index.ts (API endpoints)
    routes/
      users.ts
      data.ts
  shared/
    types.ts (User, ApiResponse, etc.)
```

## Styling Approach
- Use CSS custom properties for theming
- Simple, clean typography (system fonts)
- Subtle shadows and borders
- Calm color palette (grays, blues)
- Mobile-responsive without complexity
- Focus on readability and whitespace

## API Endpoints
- GET /api/users - List users
- GET /api/users/:id - Get user
- POST /api/data - Echo data back
- GET /api/features - List webstir features

## Notes
- Each page should have comments explaining what it demonstrates
- Include a README in the generated project
- Make sure routing metadata is properly detected
- Test all build modes after generation