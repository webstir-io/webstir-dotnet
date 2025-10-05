# Synchronize Framework Packages

This guide explains how the bundled frontend and testing packages are built and published for Webstir workspaces.

## Overview

- Run commands via `dotnet run --project Framework/Framework.csproj -- packages …` from the repo root (or use a built `framework` binary).
- `Framework/Frontend` and `Framework/Testing` contain the source for the bundled `@electric-coding-llc/webstir-frontend` and `@electric-coding-llc/webstir-test` packages.
- `framework packages sync` runs `npm ci`, `npm run build`, and `npm pack`, then copies the generated tarballs into both the package directories and `Framework/Resources/webstir` so the CLI embeds them.
- `Framework/Packaging/framework-packages.json` records the package name, version, registry specifier, and tarball metadata (file name, checksum, size, repository path). `Engine/Resources/package.json` is updated to depend on the `.webstir` tarballs so new workspaces are immediately installable offline.
- `webstir install` copies the embedded tarballs into `<workspace>/.webstir/`, pins dependencies to `file:./.webstir/<tarball>.tgz`, and only falls back to the registry when explicitly requested (for example, by setting `WEBSTIR_PACKAGE_SOURCE=registry`).

## Update The Packages
1. Bump versions inside `Framework/Frontend/package.json` or `Framework/Testing/package.json` as needed.
2. (Optional) Run `framework packages diff` to preview checksum/size differences without touching files.
3. Run `framework packages sync`.
   - Add `--frontend` or `--test` to rebuild a single package when only one changed.
   - The command rebuilds the package sources, regenerates deterministic tarballs, copies them into `Framework/Resources/webstir`, and updates both `Framework/Packaging/framework-packages.json` and `Engine/Resources/package.json`.
   - Set `WEBSTIR_FRONTEND_REGISTRY_SPEC` or `WEBSTIR_TEST_REGISTRY_SPEC` before running if you need an alternate registry specifier (for example, a dist-tag).
   - Append `--prune-webstir` to delete cached `.webstir/*.tgz` files under `Tests/out/**` and `CLI/out/**` so local sandboxes pick up the fresh tarballs on the next run.
4. Run `framework packages verify`.
   - The verifier checks that the tarballs on disk match the recorded checksums, that the embedded copies are in sync, and that `Engine/Resources/package.json` points at the correct `.webstir` archives.
5. When you are ready to publish the new versions, run `framework packages publish`.
   - Publishing pushes each tarball to the configured registry (GitHub Packages by default) if that version is missing. Export `GH_PACKAGES_TOKEN` so npm can authenticate.
6. Commit the updated package sources, tarballs (`Framework/Frontend/*.tgz`, `Framework/Testing/*.tgz`, `Framework/Resources/webstir/*.tgz`), lockfiles, and metadata files (`Framework/Packaging/framework-packages.json`, `Engine/Resources/package.json`).

## Install In A Workspace
- Run `webstir install` (or any workflow that indirectly calls it) in the consuming project.
- The installer pins `@electric-coding-llc/webstir-frontend` and `@electric-coding-llc/webstir-test` to `file:./.webstir/<tarball>.tgz`, then ensures `node_modules` contains the expected versions.
- If the version on disk differs, the workflow clears the cached packages and reruns `npm install` so the workspace aligns with the bundled tarballs.
- Use `webstir install --dry-run` to see what would change before reinstalling dependencies.
- Use `webstir install --clean` to delete cached tarballs before reinstalling from the embedded archives.

## Registry Requirements
- Framework installations prefer the embedded tarballs and do not require registry access.
- Keep `.npmrc` pointed at GitHub Packages so `framework packages publish` can authenticate. When the packages move to the public npm registry, update the registry URL and remove the auth token requirement.
- You can still set `WEBSTIR_PACKAGE_SOURCE=registry` (or `WEBSTIR_PREFER_REGISTRY=1`) to smoke-test registry installs. Missing credentials surface a clear warning that tarballs will be used instead.

## Verify Changes
- Run `./utilities/format-build.sh` before handing off; it formats code, builds the solution, executes package tests, and runs both `framework packages sync` and `framework packages verify`.
- Optionally run `framework packages publish` in a dry environment (or CI) to confirm the registry accepts the new version.
- Exercise `webstir install` (optionally with `--clean`) inside a sample workspace to verify the new packages resolve correctly and upgrade existing installations.
