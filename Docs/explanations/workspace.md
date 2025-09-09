# Workspace

Defines and manages all project paths. The engine builds an `AppWorkspace` from a root directory and uses it to locate sources, outputs, templates, and runtime artifacts. Centralizing paths keeps code simple and avoids fragile string operations.

## Goals
- Single source of truth for folders and files.
- No hardcoded strings scattered around the codebase.
- Easy to compute relative and derived paths safely.

## Roots
- Source roots
  - Client: `src/client`
  - Pages: `src/client/pages/<page>`
  - App assets: `src/client/app`
  - Images: `src/client/images`
  - Fonts: `src/client/fonts`
  - Media: `src/client/media`
  - Server: `src/server`
  - Shared: `src/shared`
  - Types: `types`
- Build roots (dev)
  - Client: `build/client`
  - Pages: `build/client/pages/<page>`
  - Images: `build/client/images`
  - Fonts: `build/client/fonts`
  - Media: `build/client/media`
  - Server: `build/server`
- Dist roots (publish)
  - Client: `dist/client`
  - Pages: `dist/client/pages/<page>`
  - Images: `dist/client/images`
  - Fonts: `dist/client/fonts`
  - Media: `dist/client/media`
  - App assets: `dist/client/app`

## Conventions
- Base HTML lives at `src/client/app/app.html` and contains a `<main>` where page HTML merges.
- Pages live under `src/client/pages/<page>/` with `index.html|css|ts` (publish may produce `index.<timestamp>.{css|js}` and `manifest.json`).
- Server entry is `src/server/index.ts` → compiled to `build/server/index.js`.
- Shared modules under `src/shared/` are consumed by both client and server.

## Constants & Helpers
- Constants live in `Engine/Constants.cs` (folders, file names, extensions).
- Do not hand-roll path strings. Use helpers:
  - `Engine.Extensions.PathExtensions`: `Combine`, `DirectoryName`, `Exists`, `Files`, `Folders`, `Create`.
  - `Engine.Extensions.DirectoryExtensions`: `CreateSubDirectory`, `CopyToAsync`, etc.
- Use `Path.GetRelativePath` for relativity where needed.

## AppWorkspace
- Created from the working directory (or an explicit path when provided by the CLI).
- Exposes typed properties for common roots (e.g., `SourceClientRoot`, `BuildClientRoot`, `DistClientRoot`).
- Provides methods to resolve page locations, outputs, and temp files.
- Validates required inputs (e.g., base HTML, server entry) and raises clear errors when missing.

## Deriving Paths (Examples)
- Page folder: `Combine(SourceClientRoot, "pages", pageName)`
- Page dev HTML: `Combine(BuildClientRoot, "pages", pageName, "index.html")`
- Page dist bundle: `Combine(DistClientRoot, "pages", pageName, "index.<timestamp>.js")`
- Server build output: `Combine(BuildServerRoot, "index.js")`
- Manifest: `Combine(DistClientRoot, "pages", pageName, "manifest.json")`

## Watching & Ignore Rules
- Watch `src/**` for changes and ignore `build/**` and `dist/**`.
- Batch events to avoid thrashing on save bursts.
- Route changes by area:
  - Client change → rebuild affected page/assets.
  - Server change → rebuild server and restart Node.
  - Shared change → rebuild both as needed.

## Contracts
- The directory structure above is part of the public contract tested by E2E tests.
- Fingerprinted assets and manifests under `dist/client/pages/<page>/` are required for publish.
- Clean URLs depend on the `pages/<page>/index.html` convention.

## Error Handling
- Missing base HTML or server entry → fail fast with actionable messages.
- Invalid page names or paths → normalized and validated before use.

## Related Docs
- Solution overview — [solution](solution.md)
- Engine internals — [engine](engine.md)
- Pipelines — [pipelines](pipelines.md)
- Dev service — [devservice](devservice.md)
- Tests — [tests](testing.md)
