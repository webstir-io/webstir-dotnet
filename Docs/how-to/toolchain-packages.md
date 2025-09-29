# Synchronize Toolchain Packages

This guide explains how the bundled frontend and testing packages are produced, recorded, and installed for projects that rely on the Webstir toolchain.

## Overview
- `framework/frontend` and `framework/testing` contain the source for the published `@electric-coding-llc/webstir-frontend` and `@electric-coding-llc/webstir-test` bundles.
- `webstir toolchain sync` rebuilds the frontend and testing packages and copies the tarballs into both `Engine/Resources/tools` (embedded with the CLI) and `framework/out` (the local package repository).
- `framework/out/manifest.json` tracks the tarball metadata so the runtime knows where to find archives on disk when registry access is unavailable.
- `Engine/Bridge/Frontend/FrontendPackageInstaller` and `Engine/Bridge/Test/TestPackageInstaller` consume the manifest and ensure the right version is unpacked into each workspace.
- `webstir install` (an alias for the `InstallWorkflow`) uses `ToolchainSynchronizer` to refresh dependencies and rerun `npm install` if the packaged versions change.

## Update The Packages
1. Update versions inside `framework/frontend/package.json` or `framework/testing/package.json` as needed.
2. Run `webstir toolchain sync` from the repo root.
   - Add `--frontend` or `--test` to rebuild a single package when only one changed.
   - The workflow runs `npm ci`, packs the tarballs, refreshes the copies in `Engine/Resources/tools` and `framework/out`, and rewrites `framework/out/manifest.json` with the new hashes.
   - If you want CLI users to prefer a registry build, set `WEBSTIR_FRONTEND_REGISTRY_SPEC` or `WEBSTIR_TEST_REGISTRY_SPEC` before running the command so the manifest includes the external specifier.
   - For CI enforcement, run `webstir toolchain sync --verify` (or `webstir toolchain verify`) to ensure the manifest and tarballs are committed.
3. Commit the updated tarballs, manifests, and any source changes.

## Install In A Workspace
- Run `webstir install` (or rerun any workflow that calls `ToolchainSynchronizer`) in the consuming project.
- The installer copies the tarballs into `.tools`, pins the dependency in `package.json`, and compares the hash recorded in the manifest to detect stale archives.
- When versions drift, the workflow clears `node_modules/@electric-coding-llc/*` and reruns `npm install`, so the bundled packages match the manifest.

## Registry And Offline Modes
- By default, installers reuse the local tarballs described in `framework/out/manifest.json`.
- Setting `WEBSTIR_PACKAGE_SOURCE=registry` or `WEBSTIR_PREFER_REGISTRY=true` tells `ToolchainSynchronizer` to prefer the registry specifier recorded in the manifest when available.
- You can point the CLI at a different on-disk repository by exporting `WEBSTIR_PACKAGE_ROOT` before running the workflow; the manifest is resolved relative to that directory.
- For CI or sandbox flows that need an `.npmrc`, see `Sandbox/npmrc` for scripts that generate the auth file and publish the local packages to a temporary registry.

## Verify Changes
- After updating packages, run `./utilities/format-build.sh` or at least `dotnet build` and `npm test` in the package you touched.
- Run `webstir install` inside a sample workspace to confirm the new tarball installs cleanly and that the desired registry preference is respected.
- Optionally, run `webstir toolchain verify` to ensure `framework/out` and `Engine/Resources/tools` remain unchanged (useful in CI after a clean checkout).
- `./utilities/format-build.sh` already includes a `webstir toolchain sync --verify` check, so running that helper before handing off guarantees manifests and tarballs are current.
