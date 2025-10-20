# Framework Packaging Plan

> Status: completed. This document is retained for historical context; the registry-first approach described here is now the default.

## Background
- Historically, `framework packages sync` rebuilt tarballs, copied them into `Framework/out` and `Framework/Resources/tools`, and then rewrote `Framework/out/manifest.json` (`Framework/Packaging/PackageBuilder.cs`, `Framework/Commands/PackageConsoleCommand.cs`).
- Runtime installers (`Framework/Packaging/FrontendPackageInstaller.cs`, `Framework/Packaging/TestPackageInstaller.cs`) read that manifest, copy tarballs into workspace `.webstir`, and verify hashes via `FrameworkPackageRepository`.
- CLI helpers (for example, `utilities/local-ci.sh`) expect `framework packages verify` to confirm the manifest and tarballs match git.
- This dual-storage model was designed for offline installs but increases maintenance, adds hash drift failure modes, and confuses contributors who expect a registry-first experience.

## Goals
- Treat the registry as the single distribution channel for `@webstir-io/*` packages.
- Simplify the build/publish workflow to mirror standard npm libraries.
- Reduce repository noise by eliminating committed tarballs and manifests.
- Improve developer ergonomics with straightforward auth guidance and clearer errors when credentials are missing.

## Non-Goals
- Enabling full offline installs after the migration.
- Redesigning the package contents themselves.
- Moving to the public npm registry (tracked separately; this plan keeps GitHub Packages until release).

## Target Architecture
### Build & Publish
- `framework packages sync` runs `npm ci`, `npm run build`, and `npm pack` inside each package workspace (e.g., `Framework/Frontend`, `Framework/Testing`), returning metadata but leaving the tarball alongside the source.
- `framework packages publish` pushes the built tarball to a configurable registry URL (default: GitHub Packages) and skips if the version already exists.
- No files are copied into `Framework/out` or `Framework/Resources/webstir`; no manifest is generated.

### Installers & CLI Workflows
- `FrontendPackageInstaller` and `TestPackageInstaller` always pin the dependency to the registry specifier (falling back to `package.json` versions if none provided).
- `PackageSynchronizer` determines whether to rerun `npm install` by comparing dependency specifiers and installed versions, not tarball hashes.
- If the registry install fails or credentials are missing, the CLI surfaces a clear error with remediation steps (token required, link to docs).

### Auth & Config
- `.npmrc` continues to point `@webstir-io` to GitHub Packages and requires `GH_PACKAGES_TOKEN` during the private phase.
- `PackageBuilder` reads the registry URL and access mode from environment variables or CLI flags to ease the eventual switch to npmjs.
- Scripts emit friendly warnings when auth variables are unset and skip publish gracefully.

## Workstreams
1. **Refactor `PackageBuilder` flow**
   - Remove copies to `Framework/out`/`Framework/Resources/webstir` and the manifest writer.
   - Simplify result objects to return build metadata and publish status only.
2. **Retire manifest consumers**
   - Delete `FrameworkPackageRepository` and related manifest parsing.
   - Rework installers to depend solely on registry specifiers and installed package.json data.
   - Trim hash validation and tarball copying logic.
3. **Update `PackageSynchronizer`**
   - Adjust install decision logic to align with the new installer outputs.
   - Remove references to tarball flags (`TarballUpdated`, `Hash` checks) and rely on dependency/ version mismatch detection.
4. **Tooling & script updates**
   - Edit `utilities/local-ci.sh` and any CI jobs to drop `framework packages verify` and manifest checks.
   - Add lightweight publish validation (e.g., `npm view`) when `framework packages publish` runs.
   - Rework `.github/workflows/release.yml` so releases no longer rely on `Framework/out` tarballs—either stop attaching them or generate fresh archives during the job.
5. **Asset cleanup**
   - Delete `Framework/out/**`, `Framework/Resources/webstir/**`, and embedded resource item groups from `Framework.csproj`.
   - Remove `Framework/out` from `.gitignore` once the directory disappears.
6. **Docs & Dev Onboarding**
   - Update `docs/how-to/package-synchronization.md` and `docs/how-to/framework-packages.md` to describe the registry-first flow and credential requirements.
   - Document how to set `GH_PACKAGES_TOKEN`, how publishing works now, and call out the future switch to the public registry.
7. **Testing & rollout**
   - Add/adjust integration tests to validate installer behavior with registry installs (mock success/failure).
   - Run through `webstir init/install/watch` manually with and without valid auth to confirm error messaging.
   - Communicate the breaking change (registry access now required) and coordinate the release after packages are rebuilt.

## Migration Notes
- Expect PRs to land in the above order to keep diffs reviewable.
- A transitional milestone can keep the manifest generation behind a feature flag if we need to support both modes briefly, but the goal is to remove it entirely once scripts/docs are updated.
- When ready to publish publicly, override the registry URL/access mode in one place (environment variable or config file) and update docs accordingly.
