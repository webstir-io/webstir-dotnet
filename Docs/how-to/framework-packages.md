# Build Framework Packages

This guide explains how maintainers rebuild the bundled frontend and testing packages that ship with Webstir.

## Overview
- `framework/frontend` and `framework/testing` contain the sources for the published `@electric-coding-llc/webstir-frontend` and `@electric-coding-llc/webstir-test` bundles.
- The separate `framework` CLI (see `framework/Framework.csproj`) rebuilds those bundles, copies the tarballs into `framework/Resources/tools` (embedded with the CLI) and `framework/out` (the local package repository), and refreshes `framework/out/manifest.json` with new hashes.
- `webstir install` uses the synchronizer to make sure consuming workspaces pull the expected tarballs or registry builds and reruns `npm install` when versions drift.

## Update The Packages
1. **Bump versions** – The release workflow runs `node ./utilities/release/bump-version.mjs --bump <patch|minor|major>` automatically. You can invoke the same script locally (add `--dry-run` to preview without touching files). The script updates both package manifests and lockfiles so the CLI reads a consistent version.
2. **Rebuild packages** – Run `dotnet run --project framework/Framework.csproj -- packages sync` from the repo root (or use the `framework` executable if you have published it).
   - Add `--frontend` or `--test` to rebuild a single package when only one changed.
   - The build command runs `npm ci`, builds, regenerates the tarballs, refreshes the copies in `framework/Resources/tools` and `framework/out`, and rewrites `framework/out/manifest.json` with the new hashes.
   - Set `WEBSTIR_FRONTEND_REGISTRY_SPEC` or `WEBSTIR_TEST_REGISTRY_SPEC` before running the command if you want the manifest to prefer registry builds (e.g., `https://npm.pkg.github.com/@electric-coding-llc/webstir-frontend`).
   - Pass `--publish` to push new tarballs to GitHub Packages when the version is missing. Ensure `GH_PACKAGES_TOKEN` is exported so npm can authenticate. The tarballs produced by the build are ignored by git; CI and the release workflow regenerate them as needed.
   - Pass `--verify` to ensure the manifest and tarballs are committed after the build.
3. **Commit artifacts** – Include the updated manifest, package manifests, and sources in your PR.

## Automate Releases
- Trigger the **Release** workflow in GitHub Actions when you are ready to publish a new framework build.
- Choose the bump type (patch, minor, or major). With the default `auto` selection, apply one of the `release:{patch|minor|major}` labels to the PR that lands the change; the workflow reads those labels and picks the highest bump automatically.
- The workflow:
  1. Bumps both package manifests and lockfiles.
  2. Runs `dotnet run --project framework/Framework.csproj -- packages sync --publish --verify` to rebuild tarballs, republish packages (if missing), and confirm hashes.
  3. Commits the manifest and version bumps back to `main`, tags `vX.Y.Z`, and creates the corresponding GitHub release with tarballs attached.
- Provide `dry_run: true` if you only want to preview the next version without publishing packages or pushing commits.

## Developer Helpers
- Run `utilities/build-packages.sh` before committing frontend/testing changes. The helper checks for tracked changes under `framework/frontend` or `framework/testing`, calls `framework packages sync` when needed, and stages the updated `framework/out/manifest.json` for you.

## Install In A Workspace
- Run `webstir install` (or any workflow that indirectly calls it) inside a consuming workspace.
- The installer copies the tarballs into `.tools`, pins the dependency in `package.json`, and compares the hash recorded in the manifest to detect stale archives.
- When versions drift, it clears `node_modules/@electric-coding-llc/*` and reruns `npm install`, keeping the workspace aligned with the bundled packages.
- Use `webstir install --dry-run` to see what would change before reinstalling dependencies.

## Registry And Offline Modes
- By default, installers reuse the local tarballs described in `framework/out/manifest.json`.
- Setting `WEBSTIR_PACKAGE_SOURCE=registry` or `WEBSTIR_PREFER_REGISTRY=true` tells the synchronizer to prefer the manifest's `registrySpecifier` when available.
- Export `WEBSTIR_PACKAGE_ROOT` to point the CLI at a different on-disk repository before running the workflow; the manifest is resolved relative to that directory.
- For CI or sandbox flows that need an `.npmrc`, see `Sandbox/npmrc` for scripts that generate the auth file and publish the local packages to a temporary registry.

## Verify Changes
- After updating packages, run `./utilities/format-build.sh` or at least `dotnet build` and `npm test` in the package you touched.
- Run `webstir install` inside a sample workspace to confirm the new tarball installs cleanly and that the desired registry preference is respected.
- Optionally, run `framework packages verify` to ensure `framework/out` and `framework/Resources/tools` remain unchanged (useful in CI after a clean checkout).
- `./utilities/format-build.sh` already includes a `framework packages sync --verify` check, so running that helper before handing off guarantees manifests and tarballs are current.
- The manifest records the UTC generation timestamp; `framework packages verify` recomputes tarball hashes to confirm the manifest matches what is on disk.
