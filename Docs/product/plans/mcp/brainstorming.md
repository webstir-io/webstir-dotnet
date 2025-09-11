# MCP Integration with Webstir

## Overview
Model Context Protocol (MCP) integration to enable AI-powered development workflows with Webstir projects.

## Core Integration Points

### 1. Build Automation Server
**Purpose**: Expose Webstir CLI commands as MCP tools
- `webstir.init` - Initialize new projects with AI-suggested configurations
- `webstir.build` - Trigger builds and receive structured error reports
- `webstir.publish` - Deploy production builds with optimization insights
- `webstir.addPage` - Generate new pages with AI-written boilerplate

**Value**: AI assistants can manage entire project lifecycle without terminal access

### 2. Live Development Assistant
**Purpose**: Real-time development feedback via SSE integration
- Connect to WatchService for file change notifications
- Stream build errors/warnings as they occur
- Suggest fixes based on error context
- Auto-apply corrections with user approval

**Value**: Proactive error resolution during development

### 3. Build Pipeline Intelligence
**Purpose**: Deep visibility into build processes
- Token-level CSS analysis (via CssTokenizer)
- JavaScript bundle composition and tree-shaking insights
- Asset manifest tracking for cache optimization
- Performance metrics per build stage

**Value**: AI can suggest optimizations based on actual build data

### 4. Code Generation Tools
**Purpose**: Generate Webstir-compliant code structures
- Create TypeScript components following project conventions
- Generate CSS modules with proper imports
- Add backend API endpoints with type safety
- Maintain project references in tsconfig files

**Value**: Consistent code generation that follows Webstir patterns

## Implementation Architecture

### MCP Server Structure
```
mcp-webstir/
├── src/
│   ├── tools/          # MCP tool definitions
│   │   ├── build.ts
│   │   ├── watch.ts
│   │   └── generate.ts
│   ├── resources/      # MCP resource providers
│   │   ├── diagnostics.ts
│   │   └── manifest.ts
│   └── server.ts       # Main MCP server
└── package.json
```

### Integration with Engine
- Direct Engine API access (bypass CLI)
- Hook into Workflow events
- Subscribe to Service notifications
- Access Pipeline internals for analysis

## Use Cases

### 1. AI-Driven Project Setup
```
User: "Create a new blog with TypeScript and CSS modules"
AI: [Uses MCP to run init, configure TypeScript, enable CSS modules]
```

### 2. Real-Time Error Resolution
```
WatchService: "TypeScript error in frontend/pages/about.ts:15"
MCP: [Streams error to AI]
AI: "Missing import for SharedType, adding it now..."
MCP: [Applies fix via edit tool]
```

### 3. Performance Optimization
```
AI: [Queries build metrics via MCP]
AI: "Your bundle includes unused exports. Enabling tree-shaking..."
MCP: [Modifies build configuration]
```

### 4. Component Scaffolding
```
User: "Add a contact form page"
AI: [Uses MCP to generate page structure]
MCP: [Creates contact.html, contact.css, contact.ts with proper imports]
```

## Technical Considerations

### SSE Bridge
- Convert Server-Sent Events to MCP notifications
- Maintain persistent connection during watch mode
- Handle reconnection logic

### TypeScript Integration
- Parse tsc output for structured errors
- Map source locations to original files
- Provide fix suggestions based on error codes

### CSS Processing Hooks
- Expose CssTokenizer for analysis
- Allow CSS transformation plugins
- Report minification savings

### Build Graph Access
- Expose module dependency graph
- Track import chains
- Identify circular dependencies

## Potential MCP Resources

### Diagnostics Resource
```json
{
  "uri": "webstir://diagnostics/current",
  "name": "Current Build Diagnostics",
  "mimeType": "application/json",
  "text": "{\"errors\": [...], \"warnings\": [...]}"
}
```

### Manifest Resource
```json
{
  "uri": "webstir://manifest/index",
  "name": "Asset Manifest for index.html",
  "mimeType": "application/json",
  "text": "{\"css\": [...], \"js\": [...], \"assets\": [...]}"
}
```

## Development Phases

### Phase 1: Basic Build Tools
- Implement core CLI commands as MCP tools
- Simple success/error reporting
- Synchronous execution model

### Phase 2: Live Development
- SSE integration for real-time feedback
- Diagnostic streaming
- File watching notifications

### Phase 3: Intelligence Layer
- Build analysis and insights
- Performance recommendations
- Code generation templates

### Phase 4: Advanced Features
- Multi-project management
- Deployment integration
- Custom plugin support

## Benefits

### For Developers
- Never leave IDE for build tasks
- AI catches errors before running builds
- Automated boilerplate generation
- Performance insights without profiling

### For AI Assistants
- Direct project control
- Rich context from build pipeline
- Structured error information
- Ability to fix issues proactively

## Open Questions

1. Should MCP server be bundled with Webstir or separate package?
2. How to handle long-running watch processes in MCP?
3. Should we expose raw Engine APIs or curated tool set?
4. How to manage authentication for production deployments?
5. Can MCP tools modify build configuration directly?

## Next Steps

1. Prototype basic MCP server with init/build tools
2. Test SSE-to-MCP bridge for watch mode
3. Design resource schema for diagnostics
4. Create example AI workflows
5. Gather feedback from early users