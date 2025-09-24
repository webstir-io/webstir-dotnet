# Framework Frontend Module Breakdown

## Scope
Document the concrete work packages required to stand up the `framework/frontend` TypeScript project before wiring the .NET CLI bridge. Each package should be independently testable so that Phase 2 can progress incrementally.

## Workstreams

### HTML Pipeline
- Port template merge logic (`app/app.html` + page fragments) into `htmlBuilder.ts`.
- Recreate validation checks for `<head>` and `<main>` fragments to match current CLI errors.
- Implement resource rewriting for publish (manifest-based asset paths, refresh script removal).
- Carry across hardening steps: SRI injection, resource hints, lazy-loading, image dimension hydration, HTML formatting.

### CSS Pipeline
- Convert `CssEsbuildAdapter` behaviors to TypeScript using the JS esbuild API.
- Provide shared PostCSS pipeline with autoprefixer + module alias (`@app`) support.
- Emit hashed filenames for production, plain files with sourcemaps for build.
- Register manifest updates and optional precompression of production outputs.

### JavaScript Pipeline
- Recreate TypeScript compile → esbuild bundle flow with split chunks in production.
- Preserve error parsing/reporting for TypeScript diagnostics (line/column level).
- Copy refresh script and update asset manifest entries for page bundles.
- Provide incremental rebuild support keyed off the changed file path.

### Static Asset Pipelines
- Implement image, font, and media copiers with the same extension whitelists as today.
- Rebuild `ImageOptimizer` in TypeScript (SVG sanitization + optional webp/avif generation + dimension probing).
- Ensure publish stage writes to `dist` and triggers precompression where applicable.

### Manifest & Tooling
- Port `AssetManifest`, `Precompression`, and related helpers (e.g., critical CSS extraction) to TypeScript.
- Generate a deterministic JSON manifest per page and surface the location for the CLI bridge.
- Provide utility wrappers for workspace paths mirroring `AppWorkspace` semantics.

## Deliverables
1. Builder modules under `framework/frontend/src/builders` for HTML, CSS, JS, and static assets.
2. Shared utilities (`assetManifest`, `precompression`, `workspace`, `logger`) consumed by the builders.
3. Integration tests (Node-level) covering template merge, manifest emission, and CLI command wiring.
4. Documentation updates describing the new package structure and incremental rollout plan.

## Current Status (Phase 2 – In Progress)
- ✅ Scaffolding complete: `javascript`, `css`, `html`, and `static-assets` builders now run during `build` and `publish`, feeding a shared pipeline orchestrator.
- ✅ JS bundling uses `esbuild` for both dev (sourcemaps) and publish (hashed filenames, precompression, manifest updates).
- ✅ CSS processing runs PostCSS+autoprefixer in dev and `csso` minification for publish, updating manifests with hashed filenames.
- ✅ HTML builder merges fragments via Cheerio, validates templates, removes the dev refresh script on publish, rewrites asset references from the manifest, and emits compressed variants.
- ✅ Static assets copy from `src/frontend/{images,fonts,media}` into `build`/`dist`.
- ✅ Changed-file hints short-circuit JS/CSS/HTML/static builders during rebuilds.
- ✅ Publish step optimizes images (WebP/AVIF) and injects intrinsic dimensions into HTML.
- ✅ Publish hardening adds SRI for external assets, resource hints, lazy loading, and inlines small critical CSS.
- ✅ Rebuilds now target only the affected page for HTML/CSS/JS when a page asset changes.
- ✅ .NET frontend worker invokes the TypeScript CLI for build/publish, keeping package artifacts in sync via embedded tarballs.
- ✅ Add-page/add-test workflows now shell out to TypeScript CLIs for scaffolding, eliminating legacy handler templates.
- ✅ CLI commands (`build`, `publish`, `rebuild`) now emit the workspace manifest and run the TypeScript pipeline with duration logging.

### Remaining Parity Gaps
- ✅ Structured diagnostics & configuration now emit JSON diagnostics from the TypeScript CLI and load feature toggles from `frontend.config.json`.
- ✅ Dev server integration uses the TypeScript CLI `rebuild` command from the .NET watch loop, preserving structured logging.
- ✅ Node-side test coverage and CI hooks ship with `npm test` (node --test) and run via `utilities/format-build.sh`.

## Open Questions
- Do we keep optional third-party CLI tools (cwebp/avifenc) or vendor Node-only fallbacks?
- Should precompression remain default-on or become configurable via the shared manifest?
- How will we surface structured diagnostics back to the .NET CLI (JSON channel vs. stdio parsing)?
