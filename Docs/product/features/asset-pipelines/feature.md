# Asset Pipelines: Images, Fonts, Media

Split the current single “assets/images” handling into three clear pipelines: Images, Fonts, and Media. Each pipeline has explicit file types, inputs/outputs, and simple rules for dev and publish builds.

## Problem
- “Assets” is vague. Images are handled today; fonts and other media are not first‑class.
- Developers need obvious folders and predictable outputs.
- Watching and copying should be fast and isolated per type.

## Goals
- Clear source folders: `images/`, `fonts/`, `media/` under `src/client/**`.
- Deterministic copy with structure preserved in `build/**` and `dist/**`.
- Simple, explicit file type allowlists per pipeline.
- Parallel, no-op safe runs; integrate with existing client build/publish flows.
- Errors are clear (folder missing is fine; unsupported type is ignored).

## Non‑Goals (v1)
- No content transforms (optimization, compression, EXIF stripping, font subsetting).
- No fingerprinting/renaming. Keep original filenames. Caching is handled by server config.
- No special HTML/CSS rewriting beyond what existing pipelines already do.

## Source Layout (Client)
- Images source: `src/client/images/**`
- Fonts source: `src/client/fonts/**`
- Media source: `src/client/media/**` (audio/video and similar)

All folders are optional. Nested folders are allowed and preserved.

## Outputs
- Dev (build):
  - `build/client/images/**`
  - `build/client/fonts/**`
  - `build/client/media/**`
- Publish (dist):
  - `dist/client/images/**`
  - `dist/client/fonts/**`
  - `dist/client/media/**`

Paths preserve the source subfolder structure exactly.

## File Types
- Images: `.png .jpg .jpeg .gif .svg .webp .ico`
- Fonts: `.woff .woff2 .ttf .otf .eot .svg`
- Media: `.mp3 .m4a .wav .ogg .mp4 .webm .mov`

Files with other extensions in these folders are ignored (no error).

## Behavior
- Build:
  - Copy allowed files from `src/client/<type>/**` → `build/client/<type>/**`.
  - Create target folders on demand; skip when source folder is missing.
- Publish:
  - Copy allowed files from `build/client/<type>/**` → `dist/client/<type>/**`.
  - Keep filenames; overwrite existing on re-run.
- Watch:
  - Changes under any of the three source folders trigger that pipeline’s copy only.
  - Dev server serves these as static files with standard caching (no HTML caching).

## Orchestration
- Client build order: JS → parallel (HTML, CSS, Images, Fonts, Media).
- Client publish order: CSS + JS → HTML → Images → Fonts → Media → App assets.
- Pipelines must be idempotent and safe to run multiple times.

## Engine API & Paths
- Add folder constants:
  - `Folders.Fonts = "fonts"`
  - `Folders.Media = "media"`
- Add workspace paths (mirroring Images):
  - `ClientFontsPath`, `ClientBuildFontsPath`, `ClientDistFontsPath`
  - `ClientMediaPath`, `ClientBuildMediaPath`, `ClientDistMediaPath`
- Add file extensions (if helpful): `.woff .woff2 .ttf .otf .eot`, audio/video as above.

## Handlers
- New folders under `Engine.Pipelines` (each with its own handler):
  - `Engine.Pipelines.Images.ImagesHandler` — image allowlist copy.
  - `Engine.Pipelines.Fonts.FontsHandler` — font allowlist copy.
  - `Engine.Pipelines.Media.MediaHandler` — media allowlist copy.
- Rename/keep `AssetHandler` as `ImagesHandler` and locate it under `Engine/Pipelines/Images`.
- Methods per handler:
  - `Task BuildAsync()`
  - `Task PublishAsync()`
  - `static Task AddPageAsync(string pageName)` returns completed task (no page coupling).

## ClientWorker Integration
- Update constructor to accept three handlers: `ImagesHandler`, `FontsHandler`, `MediaHandler`.
- Build: run `images.BuildAsync`, `fonts.BuildAsync`, `media.BuildAsync` in parallel with `html` and `css`.
- Publish: run `images.PublishAsync`, `fonts.PublishAsync`, `media.PublishAsync` after HTML.

## Errors & Diagnostics
- Missing source folders: no error; pipeline is a no-op.
- Permission or copy failures: surface a clear message naming the pipeline and file.
- Unsupported extensions: ignore silently; optional debug log.

## Serving & Caching
- Dev: serve from `build/client/**` with short/disable caching for quick iteration.
- Publish: infra (e.g., nginx) can set cache for static assets (e.g., 7 days). No fingerprinting in v1.

## Testing (per Webstir philosophy)
- Workflow: init → add sample `images/`, `fonts/`, `media/` → `build` → verify copies under `build/client/**`.
- Workflow: `publish` → verify copies under `dist/client/**`.
- Property: arbitrary nesting preserved; filenames intact.
- Negative: folders absent → no errors; unknown extensions ignored.
- Dev server: request a copied asset path returns 200 (smoke).

## Migration
- Today’s `AssetHandler` handles images only. Migrate by:
  - Introduce `ImagesHandler` (move logic and name explicitly) or keep `AssetHandler` but scope to images.
  - Add `FontsHandler` and `MediaHandler` with the same pattern.
  - Update `ClientWorker` wiring and DI registration.
  - Keep `src/client/images` behavior unchanged to avoid breaking changes.

## Acceptance Criteria
- New folders are recognized: `src/client/fonts`, `src/client/media`.
- `build` copies allowed files to `build/client/<type>` with structure preserved.
- `publish` copies allowed files to `dist/client/<type>` with structure preserved.
- ClientWorker runs the three pipelines without blocking others; runs are idempotent.
- Clear errors on copy failures; missing folders are safe no-ops.

## Open Questions
- Do we want basic optimizations (e.g., image compression) behind a flag later?
- Should we add optional hash/fingerprint in v2 to enable immutable caching?
- Any server assets that need similar handling, or client-only for now?
