# Webstir Framework Distribution & Guardrails Plan

## Goals
- Decouple the CLI binary from TypeScript tool updates while keeping installs deterministic.
- Provide fast, reliable guardrails that detect version drift before workflows execute.
- Expose light-touch extension hooks so advanced use cases stay within the supported path.
- Keep the developer workflow simple: `init`, `build`, `watch`, `publish` remain turnkey.

## Current State
- TypeScript tooling is bundled as embedded tarballs produced during CLI packaging.
- Generated workspaces rely on those embedded artifacts; version mismatches are invisible until failures occur.
- Extension points are implicit (manual edits or forks) rather than first-class contracts.

## Pain Points
- Every framework tweak forces a full CLI rebuild and republish.
- Large binaries accumulate multiple package payloads, slowing iteration and distribution.
- Missing version checks allow silent drift between the CLI and installed packages.
- Lack of sanctioned hook points encourages unsupported changes or forks.

## Proposed Architecture

### Phase 1 — Local Package Repository
- Build framework packages into `framework/out/<package-name>/<version>/` during CLI preparation.
- Generate a `framework/out/manifest.json` containing name, version, checksum, and relative tarball path for each package.
- Update CLI workflows (`init`, `build`, `publish`, `test`) to install packages from the local repository if versions are missing or stale.
- Retain current embedded tarballs only as a temporary fallback until the new installer path is stable.

### Phase 2 — Version Guardrails
- Embed the manifest (or a checksum) into the CLI so workflows know the expected package versions.
- On each workflow start, read the generated project’s lockfile or `package.json` and compare against the embedded manifest.
- Surface actionable diagnostics when versions diverge (e.g., “Run `webstir install` to sync packages”).
- Provide a dedicated CLI command (`install` or `sync-packages`) to pull the correct versions from the local repository.

### Phase 3 — Extension Hooks
- Define supported hook points in a shared config (e.g., `webstir.config.ts`):
  - Pre/post pipeline scripts for HTML, CSS, JS, assets.
  - Optional adapters for additional file types or transformations.
- Document the contract: hooks receive paths, config, and the AppWorkspace helper; return success/failure diagnostics.
- Ensure hooks run after version checks so custom logic always targets the correct package versions.

### Phase 4 — Optional npm Publishing (Future)
- Publish the same artifacts to npm when ready; reuse the manifest to point either at the local cache or registry URLs.
- Allow the CLI to prefer registry installs while falling back to on-disk packages when offline.
- Keep Phase 1–3 behavior intact so adopting npm is a configuration flip, not a restructuring project.

## Implementation Checklist
1. Scaffold `framework/out` build scripts and manifest writer.
2. Update CLI packaging to include the manifest instead of large tarballs.
3. Teach `init` to copy the manifest into new workspaces.
4. Implement package installer that reads the manifest and installs tarballs from disk.
5. Add version comparison logic and friendly error messages.
6. Introduce `webstir install` (or similar) command for manual resync.
7. Design and document hook interfaces; wire them into frontend pipelines as an initial integration point.
8. Backfill tests covering manifest parsing, installer behavior, and hook execution ordering.

## Risks & Mitigations
- **Stale local cache**: include checksums in the manifest and re-run package builds when sources change.
- **Installer performance**: cache extraction results per version to avoid reinstalling unchanged packages.
- **Hook abuse**: document scope and guard with timeouts/logging so misbehaving hooks surface clearly.
- **Future npm migration**: keep the manifest schema registry-aware (supporting `tarballPath` and `registryUrl`).

## Success Metrics
- CLI binary size reduced by removing embedded packages.
- Workflow exits early with clear guidance when versions drift.
- Developers can add approved pipeline tweaks without forking the framework.
- Transitioning to npm publishing later requires only installer configuration changes.

## Open Questions
- Where should the local repository live in released artifacts (alongside the CLI binary or separate archive)?
- Do we want a global cache across projects or per-workspace storage?
- Should hooks run in-process (TypeScript transpiled on the fly) or as isolated Node invocations for safety?
- How will the manifest be regenerated in CI, and how do we ensure it stays in sync with package builds?
