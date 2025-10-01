# Build Framework Packages

This guide explains how maintainers rebuild the frontend and testing packages that ship with Webstir.

## Overview
- `framework/frontend` and `framework/testing` contain the sources for the published `@electric-coding-llc/webstir-frontend` and `@electric-coding-llc/webstir-test` packages.
- The standalone `framework` console rebuilds those packages, updates `framework/Packaging/framework-packages.json` with the latest versions, and keeps `Engine/Resources/package.json` in sync so workspace templates reference the same releases.
- `webstir install` uses `PackageSynchronizer` to ensure consuming workspaces pin the expected registry versions and reruns `npm install` when they drift.

## Update The Packages
1. **Bump versions** – The release workflow runs `node ./framework/Scripts/bump-version.mjs --bump <patch|minor|major>` automatically. If you want to bump manually, run the script directly (add `--dry-run` to preview without touching files).
2. **Rebuild packages** – Run `dotnet run --project framework/Framework.csproj -- packages sync` from the repo root (or invoke the published `framework` binary).
   - Add `--frontend` or `--test` to rebuild a single package.
   - The command runs `npm ci`, `npm run build`, and `npm pack` in each package directory, regenerating the tarball next to `package.json`.
   - It also updates `framework/Packaging/framework-packages.json` and `Engine/Resources/package.json` so the CLI embeds the new registry specifiers.
   - Set `WEBSTIR_FRONTEND_REGISTRY_SPEC` or `WEBSTIR_TEST_REGISTRY_SPEC` before running if you need to override the default `<name>@<version>` specifier (for example, to target a dist-tag).
3. **Publish packages** – Run `./framework/Scripts/publish.sh` (optionally add `--bump minor|major`; default is patch) to bump versions and push any missing releases to the configured registry (GitHub Packages by default). Append `--dry-run` to preview the next version without publishing; the helper checks `GH_PACKAGES_TOKEN` and, if present, points `NPM_CONFIG_USERCONFIG` at the repo’s `.npmrc` automatically.
4. **Commit artifacts** – Include the updated package sources, lockfiles, `framework/Packaging/framework-packages.json`, and `Engine/Resources/package.json` in your PR.

## Automate Releases
- Trigger the **Release** workflow in GitHub Actions when you are ready to publish a new build.
- Choose the bump type (patch, minor, or major). With the default `auto` selection, apply a `release:{patch|minor|major}` label to the PR; the workflow picks the highest bump present.
- The workflow:
  1. Bumps both package manifests and lockfiles.
  2. Runs `dotnet run --project framework/Framework.csproj -- packages publish` to rebuild and publish any missing versions.
  3. Commits the updated metadata back to `main`, tags `vX.Y.Z`, and creates the corresponding GitHub release (no tarball attachments required).
- Provide `dry_run: true` if you only want to preview the next version without publishing or pushing changes.

## Developer Helpers
- Before committing frontend/testing changes, run `dotnet run --project framework/Framework.csproj -- packages sync` to ensure local build artifacts and metadata reflect the latest source.
- Use `node ./framework/Scripts/bump-version.mjs --bump patch --dry-run` to preview the next version without touching files.

## Install In A Workspace
- Run `webstir install` (or any workflow that calls it) inside a consuming workspace.
- The installer pins dependencies in `package.json` to the registry specifiers recorded in `framework/Packaging/framework-packages.json` and reruns `npm install` when those versions change.
- If a different version is already installed, the workflow removes the cached packages and reinstalls them from the registry.
- Use `webstir install --dry-run` to inspect upcoming changes.

## Registry Notes
- The CLI now requires registry access. Offline installs are no longer supported once the registry-first workflow is enabled.
- During the private phase, keep `.npmrc` pointed at GitHub Packages and ensure `GH_PACKAGES_TOKEN` is available. When ready to publish publicly, update the registry URL and remove the token requirement.
- For Sandbox or CI scenarios, create a `.npmrc` that includes the GitHub Packages token (see `Sandbox/README.md` for guidance) before running workflows that need registry access.

## Verify Changes
- Run `./utilities/format-build.sh` (or at minimum `dotnet build` plus package tests) before handing off a change; the script now calls `framework packages sync` automatically.
- In a throwaway workspace, run `webstir install` to confirm the new versions resolve correctly and that `npm install` finishes without manual intervention.
- Optionally run `framework packages publish` in a dry environment to ensure credentials are configured before the release workflow executes.
