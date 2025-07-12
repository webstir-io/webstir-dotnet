# Webstir Demo Application

This demo application showcases all the features of the Webstir framework.

## Getting Started

1. Make sure you have Node.js installed
2. Run `npm install` to install dependencies
3. Run `webstir` to start the development server
4. Open http://localhost:8088 in your browser

## Features Demonstrated

### Client-Side
- Multiple pages with navigation
- Client-side routing (About and Features pages are SPA-enabled)
- TypeScript modules and imports
- Shared types from `@shared`
- CSS styling (app-level and page-level)
- Hot reload in development

### Server-Side
- Node.js HTTP API server
- TypeScript on the server
- API routes with proper typing
- Shared types between client and server

### Build System
- Automatic TypeScript compilation
- File watching and hot reload
- API proxy (all `/api/*` requests are forwarded to the Node.js server)

## Project Structure

```
src/
  client/           # Frontend code
    app/           # App-level files (HTML, CSS, TS)
    pages/         # Individual pages
      home/        # Traditional navigation page
      about/       # SPA page with route handler
      features/    # SPA page with route handler
      api-demo/    # API demonstration page
  server/          # Backend code
    index.ts       # HTTP server setup
    routes/        # API routes
  shared/          # Shared TypeScript types
    types.ts       # Type definitions used by both client and server
```

## Try It Out

1. Edit any file and see hot reload in action
2. Navigate between pages to see SPA vs traditional navigation
3. Click buttons on the API Demo page to test the API proxy
4. Modify the shared types and see TypeScript catch any issues

## Commands

- `webstir` - Start development server
- `webstir build` - Build for development
- `webstir publish` - Build for production
- `webstir add-page <name>` - Add a new page