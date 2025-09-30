# Synchronize Framework Packages

This guide explains how the bundled frontend and testing packages are built and published for Webstir workspaces.

## Overview

> Run commands via `dotnet run --project framework/Framework.csproj -- packages …` from the repo root (or use a built `framework` binary).
- `framework/frontend` and `framework/testing` contain the source for the published `@electric-coding-llc/webstir-frontend` and `@electric-coding-llc/webstir-test` packages.
- `framework packages sync` runs `npm ci`, `npm run build`, and `npm pack` inside each package workspace so you can inspect the unpublished tarballs locally.
- `framework/Packaging/framework-packages.json` records the package name, version, and registry specifier embedded in the CLI. `Engine/Resources/package.json` is kept in sync so new workspaces pin the same versions.
- `webstir install` (via `PackageSynchronizer`) ensures workspace dependencies match those registry specifiers and reruns `npm install` when they drift.

## Update The Packages
1. Bump versions inside `framework/frontend/package.json` or `framework/testing/package.json` as needed.
2. Run `framework packages sync`.
   - Add `--frontend` or `--test` to rebuild a single package when only one changed.
   - The command rebuilds the package sources, regenerates the tarball in-place, and updates both `framework/Packaging/framework-packages.json` and `Engine/Resources/package.json`.
   - Set `WEBSTIR_FRONTEND_REGISTRY_SPEC` or `WEBSTIR_TEST_REGISTRY_SPEC` before running if you need an alternate specifier (for example, a dist-tag or private registry URL).
3. When you are ready to publish the new versions, run `framework packages publish`.
   - Publishing pushes each tarball to the configured registry (GitHub Packages by default) if that version is missing. Export `GH_PACKAGES_TOKEN` so npm can authenticate.
4. Commit the updated package sources, lockfiles, and metadata files (`framework/Packaging/framework-packages.json`, `Engine/Resources/package.json`).

## Install In A Workspace
- Run `webstir install` (or any workflow that indirectly calls it) in the consuming project.
- The installer pins `@electric-coding-llc/webstir-frontend` and `@electric-coding-llc/webstir-test` in `package.json` to the registry specifiers recorded in the catalog, then ensures `node_modules` contains the expected versions.
- If the version on disk differs, the workflow clears the cached packages and reruns `npm install` so the workspace aligns with the bundled versions.
- Use `webstir install --dry-run` to see what would change before reinstalling dependencies.

## Registry Requirements
- Framework installations always use the configured npm registry. Offline installs are no longer supported once the registry-first workflow is in place.
- Keep `.npmrc` pointed at GitHub Packages during development. When the packages move to the public npm registry, update the registry URL and remove the auth token requirement.
- If registry authentication is missing, the CLI surfaces guidance to set `GH_PACKAGES_TOKEN` (or the appropriate credential).

## Verify Changes
- Run `./utilities/format-build.sh` before handing off; it formats code, builds the solution, executes package tests, and invokes `framework packages sync`.
- Optionally run `framework packages publish` in a dry environment (or CI) to confirm the registry accepts the new version.
- Exercise `webstir install` inside a sample workspace to verify the new packages resolve correctly and upgrade existing installations.
