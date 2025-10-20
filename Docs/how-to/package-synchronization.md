# Synchronize Framework Packages

This guide covers the end-to-end workflow for rebuilding and publishing the framework packages that Webstir workspaces consume.

## Overview

- Run commands via `dotnet run --project Framework/Framework.csproj -- packages …` from the repo root (or use a built `framework` binary).
- `Framework/Frontend`, `Framework/Backend`, and `Framework/Testing` contain the sources for the published packages.
- `framework packages sync` rebuilds those packages, updates `Framework/Packaging/framework-packages.json`, and refreshes `Engine/Resources/package.json` with caret specifiers.
- `webstir install` keeps consuming workspaces aligned with the recorded registry versions by updating `package.json` specifiers and running `npm install` when drift is detected.

## Update The Packages
1. Run `framework packages bump` (for example, `framework packages bump --bump minor` or `framework packages bump --set-version 1.2.3`). Add `--dry-run` to preview the next version without touching manifests.
2. (Optional) Run `framework packages diff` to preview version or specifier drift without modifying files.
3. Run `framework packages sync`.
   - Add `--frontend`, `--testing`, or `--backend` to rebuild a single package when only one changed.
   - The command runs `npm ci` and `npm run build`, then rewrites the manifest and template dependencies with the new versions and caret specifiers. No tarballs are generated.
   - Set `WEBSTIR_FRONTEND_REGISTRY_SPEC`, `WEBSTIR_TEST_REGISTRY_SPEC`, or `WEBSTIR_BACKEND_REGISTRY_SPEC` before running if you need an alternate registry specifier (for example, a dist-tag).
4. Run `framework packages verify`.
   - The verifier ensures package directories, manifest entries, template dependencies, and the repository state are aligned.
   - The check also confirms that no legacy tarball assets remain in the repo.
5. When you are ready to publish the new versions, run `framework packages publish`.
   - Publishing pushes each package to the configured registry (GitHub Packages by default) if that version is missing. Export `GH_PACKAGES_TOKEN` so npm can authenticate.
6. Commit the updated package sources, lockfiles, `Framework/Packaging/framework-packages.json`, and `Engine/Resources/package.json`.

## Install In A Workspace
- Run `webstir install` (or any workflow that indirectly calls it) in the consuming project.
- The installer rewrites the framework package entries in `package.json`, clears stale caches when necessary, and runs `npm install` so `node_modules` matches the recorded registry versions.
- Use `webstir install --dry-run` to see what would change before reinstalling dependencies.
- Use `webstir install --clean` to delete the cached `.webstir/` directory before reinstalling.

## Registry Requirements
- Framework installations now rely on registry packages. Configure `.npmrc` with the appropriate registry URL and credentials (currently GitHub Packages via `GH_PACKAGES_TOKEN`).
- Provide the token and `.npmrc` to CI or sandbox environments before executing `framework packages publish` or `webstir install`.

## Verify Changes
- Run `./utilities/format-build.sh` before handing off; it formats code, builds the solution, and executes frontend package tests.
- Run `framework packages sync` followed by `framework packages verify` whenever package artifacts change. Use `framework packages publish --dry-run` to exercise the end-to-end pipeline without modifying files.
- Optionally run `framework packages publish` in a dry environment (or CI) to confirm the registry accepts the new version.
- Exercise `webstir install` (optionally with `--clean`) inside a sample workspace to verify the new packages resolve correctly and upgrade existing installations.
