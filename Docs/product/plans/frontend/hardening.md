# Frontend Pipeline Hardening Plan

## Overview
Practical improvements to make frontend build pipelines more reliable and faster without overengineering.

## Goals
- Fail fast with clear errors
- Speed up builds through parallelization
- Add only essential production features
- Keep it simple and maintainable

## Phase 1: Error Handling

### Consistent Error Reporting
- Use ILogger directly for all error reporting (remove DiagnosticCollection)
- Return bool from pipeline methods to signal success/failure
- Include file path and line number in all error messages
- Standardized error format: `[Pipeline] Error in file:line - message`

### Error Collection Strategy
- Run all handlers within a build group to collect all errors
- Stop at first failing group (don't continue to next build order)
- Show all errors before stopping build
- No unhandled exceptions - catch and log everything

### Build Safety
- Verify critical files exist before processing
- Return false on errors, true on success
- Workers check return values and stop appropriately

## Phase 2: Performance

### Parallel Execution
- Run Images/Fonts/Media copying in parallel (they're independent)
- Run CSS and JS bundling in parallel after TypeScript
- Simple implementation: `await Task.WhenAll(cssTask, jsTask, imagesTask)`

### Incremental TypeScript
- Use `tsc --incremental` with tsBuildInfo cache
- Skip compilation if no .ts files changed

### Parallel Compression
- Compress multiple files simultaneously: `Task.WhenAll(files.Select(Compress))`
- Keep Brotli only (no gzip)

## Phase 3: Essential Production Features

### Security Basics
- Generate CSP header with nonces for inline scripts
- Add SRI hashes for external resources only
- Sanitize paths to prevent directory traversal

### Modern Formats (Optional)
- WebP generation for images > 50KB
- Simple implementation using existing libraries
- Keep original formats as fallback

### Simple Monitoring
- Log build time at end: `Build completed in 2.3s`
- Show final asset sizes: `JS: 45KB, CSS: 12KB`
- One-line summary per pipeline

## Phase 4: Practical Improvements

### Build Validation
- Check that HTML has `<head>` and `<body>`
- Verify JS files aren't empty
- Ensure CSS doesn't exceed reasonable size (e.g., 500KB)
- Fail fast with clear error if validation fails

### Source Maps
- Add production source maps (but gitignore them)
- Simple flag to enable/disable

### Basic Configuration
- Environment variables for key settings:
  - `WEBSTIR_PARALLEL_BUILDS=true`
  - `WEBSTIR_COMPRESSION_LEVEL=high|normal|fast`
  - `WEBSTIR_SOURCE_MAPS=true`
- No complex config files

## Implementation Priority
1. **Now**: Consistent error handling, parallel execution
2. **Soon**: Incremental TypeScript, parallel compression
3. **Later**: WebP generation, CSP headers
4. **Maybe**: Additional monitoring, configuration options

## Non-Goals
- Complex logging systems
- Extensive fallback mechanisms
- Academic-level optimization
- Overly configurable pipelines
- Dashboard or visualization tools
- Automatic error recovery
- Detailed diagnostics that clutter output