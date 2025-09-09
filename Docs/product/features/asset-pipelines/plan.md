# Asset Pipelines — Implementation Plan

Objective: Implement separate Images, Fonts, and Media pipelines (copy-only v1) with clear folders, paths, and engine wiring for dev (build/watch) and publish.

## Deliverables
- Engine support for `images/`, `fonts/`, `media/` (src → build → dist).
- Three handlers: ImagesHandler, FontsHandler, MediaHandler.
- Updated `ClientWorker` orchestration and DI wiring.
- Dev server static file support for new types.
- Updated docs (explanations/pipelines) and a lightweight test plan.

## Plan
1) Constants & Paths
- Add folder constants: `Folders.Fonts = "fonts"`, `Folders.Media = "media"`.
- Add file extensions in `FileExtensions`: `.woff .woff2 .ttf .otf .eot .mp3 .m4a .wav .ogg .mp4 .webm .mov`.
- Extend `AppWorkspace` with fonts/media paths:
  - `ClientFontsPath`, `ClientBuildFontsPath`, `ClientDistFontsPath`.
  - `ClientMediaPath`, `ClientBuildMediaPath`, `ClientDistMediaPath`.

2) Handlers (separate pipeline folders)
- Move to dedicated folders under `Engine.Pipelines`:
  - `Engine/Pipelines/Images/ImagesHandler.cs`
  - `Engine/Pipelines/Fonts/FontsHandler.cs`
  - `Engine/Pipelines/Media/MediaHandler.cs`
- Rename `AssetHandler` → `ImagesHandler` (class + file) and keep existing behavior (image allowlist).
- Add `FontsHandler` (copy allowlist to build/dist; no-op when absent).
- Add `MediaHandler` (copy allowlist to build/dist; no-op when absent).
- Keep `AddPageAsync` as completed task for all three (not page-coupled).

3) ClientWorker
- Update constructor to accept `ImagesHandler images`, `FontsHandler fonts`, `MediaHandler media`.
- Build: run `html`, `css`, plus `images`, `fonts`, `media` in parallel; preserve existing JS order.
- Build (watch optimization): if `changedFilePath` is under `images/`, `fonts/`, or `media/` (or matches their extensions), only run that pipeline; otherwise run full set.
- Publish: run CSS/JS → HTML → `images` → `fonts` → `media` → app assets (unchanged behavior for app assets).

4) DI Wiring
- Update `CLI/Program.cs` registrations and namespaces:
  - Replace `AssetHandler` with `ImagesHandler` (from `Engine.Pipelines.Images`).
  - Add `FontsHandler` (from `Engine.Pipelines.Fonts`).
  - Add `MediaHandler` (from `Engine.Pipelines.Media`).

5) Dev Server
- Extend `WebServer.IsStaticAsset` to include font and media extensions so they get static cache headers in dev.

6) Docs
- Update `docs/explanations/pipelines.md` to reflect three asset pipelines and target paths.
- Link from solution/engine docs if needed.

7) Testing (per Webstir philosophy)
- Build workflow: seed sample files under `src/client/{images,fonts,media}`; run `build`; verify copied files exist in `build/client/**` with structure preserved.
- Publish workflow: run `publish`; verify files exist in `dist/client/**` with structure preserved.
- Watch: touch a file under each folder and observe only that pipeline’s outputs change (smoke via timestamps/size).
- Dev server: request a known asset path returns 200.

8) Rollout
- Backward compatible: existing `images/` continues to work (handler renamed but behavior same).
- No fingerprinting or transforms in v1; consider as follow-ups.

## TODO
- [ ] Add `Folders.Fonts` and `Folders.Media` in `Engine/Constants.cs`.
- [ ] Add font/media extensions in `FileExtensions`.
- [ ] Extend `AppWorkspace` with fonts/media paths.
- [ ] Move/rename `Engine/Pipelines/Assets/AssetHandler.cs` → `Engine/Pipelines/Images/ImagesHandler.cs` (class + file).
- [ ] Add `Engine/Pipelines/Fonts/FontsHandler.cs`.
- [ ] Add `Engine/Pipelines/Media/MediaHandler.cs`.
- [ ] Update `Engine/Workers/ClientWorker.cs` (constructor, build/publish flows, watch filtering).
- [ ] Update DI in `CLI/Program.cs` (register `ImagesHandler`, `FontsHandler`, `MediaHandler`).
- [ ] Extend `Engine/Servers/WebServer.cs` static asset detection for new extensions.
- [ ] Update `docs/explanations/pipelines.md` to describe images/fonts/media flows.
- [ ] Smoke test: build → verify `build/client/{images,fonts,media}`.
- [ ] Smoke test: publish → verify `dist/client/{images,fonts,media}`.

## Notes & Risks
- Keep changes mechanical and behavior-preserving outside new folders.
- Missing source folders should be no-ops; copy failures must surface clear errors.

## Out of Scope (v1)
- Optimization (compression, subsetting), fingerprinting/renaming, manifest entries.

## Open Questions
- Do we want optional compression/fingerprints in v2?
- Any server-side asset needs now, or client-only?
