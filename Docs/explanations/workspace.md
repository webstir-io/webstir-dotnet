# Workspace

Defines and manages all project paths. The engine builds an `AppWorkspace` from a root directory and uses it to locate sources, outputs, templates, and runtime artifacts. Centralizing paths keeps code simple and avoids fragile string operations.

## Goals
- Single source of truth for folders and files.
- No hardcoded strings scattered around the codebase.
- Easy to compute relative and derived paths safely.

## Roots
- Source roots
  - Frontend: `src/frontend`
  - Pages: `src/frontend/pages/<page>`
  - App assets: `src/frontend/app`
  - Images: `src/frontend/images`
  - Fonts: `src/frontend/fonts`
  - Media: `src/frontend/media`
  - Backend: `src/backend`
  - Shared: `src/shared`
  - Types: `types`
- Build roots (dev)
  - Frontend: `build/frontend`
  - Pages: `build/frontend/pages/<page>`
  - Images: `build/frontend/images`
  - Fonts: `build/frontend/fonts`
  - Media: `build/frontend/media`
  - Backend: `build/backend`
- Dist roots (publish)
  - Frontend: `dist/frontend`
  - Pages: `dist/frontend/pages/<page>`
  - Images: `dist/frontend/images`
  - Fonts: `dist/frontend/fonts`
  - Media: `dist/frontend/media`
  - App assets: `dist/frontend/app`

## Conventions
- Base HTML lives at `src/frontend/app/app.html` and contains a `<main>` where page HTML merges.
- Pages live under `src/frontend/pages/<page>/` with `index.html|css|ts` (publish may produce `index.<timestamp>.{css|js}` and `manifest.json`).
- Backend entry is `src/backend/index.ts` → compiled to `build/backend/index.js`.
- Shared modules under `src/shared/` are consumed by both frontend and backend.

## Constants & Helpers
- Constants live in `Engine/Constants.cs` (folders, file names, extensions).
- Do not hand-roll path strings. Use helpers:
  - `Engine.Extensions.PathExtensions`: `Combine`, `DirectoryName`, `Exists`, `Files`, `Folders`, `Create`.
  - `Engine.Extensions.DirectoryExtensions`: `CreateSubDirectory`, `CopyToAsync`, etc.
- Use `Path.GetRelativePath` for relativity where needed.

## AppWorkspace
- Created from the working directory (or an explicit path when provided by the CLI).
- Exposes typed properties for common roots (e.g., `FrontendBuildPath`, `BackendBuildPath`, `FrontendDistPath`).
- Provides methods to resolve page locations, outputs, and temp files.
- Validates required inputs (e.g., base HTML, server entry) and raises clear errors when missing.

## Deriving Paths (Examples)
- Page folder: `Combine(FrontendPath, "pages", pageName)`
- Page dev HTML: `Combine(FrontendBuildPagesPath, pageName, "index.html")`
- Page dist bundle: `Combine(FrontendDistPagesPath, pageName, "index.<timestamp>.js")`
- Backend build output: `Combine(BackendBuildPath, "index.js")`
- Manifest: `Combine(FrontendDistPagesPath, pageName, "manifest.json")`

## Watching & Ignore Rules
- Watch `src/**` for changes and ignore `build/**` and `dist/**`.
- Batch events to avoid thrashing on save bursts.
- Route changes by area:
  - Frontend change → rebuild affected page/assets.
  - Backend change → rebuild backend and restart Node.
  - Shared change → rebuild both as needed.

## Contracts
- The directory structure above is part of the public contract tested by E2E tests.
- Fingerprinted assets and manifests under `dist/frontend/pages/<page>/` are required for publish.
- Clean URLs depend on the `pages/<page>/index.html` convention.

## Error Handling
- Missing base HTML or backend entry → fail fast with actionable messages.
- Invalid page names or paths → normalized and validated before use.

## Related Docs
- Solution overview — [solution](solution.md)
- Engine internals — [engine](engine.md)
- Pipelines — [pipelines](pipelines.md)
- Dev service — [devservice](devservice.md)
- Tests — [tests](testing.md)
