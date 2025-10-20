# Build Framework Packages

This guide explains how maintainers rebuild the frontend, backend, and testing packages that ship with Webstir.

## Overview
- `Framework/Frontend`, `Framework/Backend`, and `Framework/Testing` contain the sources for the published `@webstir-io/*` packages.
- The standalone `framework` console rebuilds those packages, records registry metadata in `Framework/Packaging/framework-packages.json`, and updates `Engine/Resources/package.json` so workspace templates stay in sync.
- `webstir install` keeps consuming workspaces aligned with the recorded registry versions by updating `package.json` specifiers and running `npm install` when drift is detected.

## Update The Packages
1. **Bump versions** – Run `dotnet run --project Framework/Framework.csproj -- packages bump` (append `--bump <patch|minor|major>` or `--set-version <x.y.z>`). Add `--dry-run` to preview the next version without touching manifests. The publish workflow invokes the same command automatically.
2. **Optional diff** – Run `dotnet run --project Framework/Framework.csproj -- packages diff` to compare the current registry metadata with the package sources. A non-zero exit code indicates version, registry specifier, or workspace specifier drift.
3. **Rebuild packages** – Run `dotnet run --project Framework/Framework.csproj -- packages sync` from the repo root (or invoke the published `framework` binary).
   - Add `--frontend`, `--testing`, or `--backend` to rebuild a single package.
   - The command runs `npm ci` and `npm run build` in each package directory, then rewrites `framework-packages.json` and `Engine/Resources/package.json` with the new versions and caret specifiers.
   - Set `WEBSTIR_FRONTEND_REGISTRY_SPEC`, `WEBSTIR_TEST_REGISTRY_SPEC`, or `WEBSTIR_BACKEND_REGISTRY_SPEC` before running if you need to override the default `<name>@<version>` registry specifier (for example, to target a dist-tag during validation).
4. **Verify metadata** – Run `dotnet run --project Framework/Framework.csproj -- packages verify` to confirm that the package sources, manifest entries, template dependencies, and repository state are aligned. The verifier also ensures no legacy tarball assets remain.
5. **Publish packages** – Run `dotnet run --project Framework/Framework.csproj -- packages publish` to bump versions, rebuild metadata, and publish any missing releases. Add `--dry-run` to preview without touching files or hitting the registry, and pass `--bump <patch|minor|major>` (or `--set-version <x.y.z>`) when you need to override the automatic bump detection. Ensure `GH_PACKAGES_TOKEN` is available before attempting to publish; the command surfaces actionable errors if authentication or `.npmrc` configuration is missing.
6. **Commit artifacts** – Include the updated package sources, lockfiles, `Framework/Packaging/framework-packages.json`, and `Engine/Resources/package.json` in your PR. No tarballs are generated or committed in the registry-only flow.

## Automate Releases
- Trigger the **Release** workflow in GitHub Actions when you are ready to publish a new build.
- Choose the bump type (patch, minor, or major). With the default `auto` selection, apply a `release:{patch|minor|major}` label to the PR; the workflow picks the highest bump present.
- The workflow:
  1. Bumps package manifests and lockfiles.
  2. Runs `dotnet run --project Framework/Framework.csproj -- packages publish` to rebuild metadata and publish any missing versions.
  3. Commits the updated metadata back to `main`, tags `vX.Y.Z`, and creates the corresponding GitHub release.
- Provide `dry_run: true` if you only want to preview the next version without publishing or pushing changes.

## Developer Helpers
- Run `dotnet run --project Framework/Framework.csproj -- packages diff` to see how the current sources compare to the recorded manifest before touching tracked files.
- Before committing frontend/testing/back-end changes, run `dotnet run --project Framework/Framework.csproj -- packages sync` followed by `dotnet run --project Framework/Framework.csproj -- packages verify` to ensure the metadata and template dependencies are in sync.
- Use `dotnet run --project Framework/Framework.csproj -- packages bump --dry-run --bump patch` to preview the next version without touching files.

## Install In A Workspace
- Run `webstir install` (or any workflow that calls it) inside a consuming workspace.
- The installer rewrites the workspace `package.json` entries for the framework packages, removes stale caches when necessary, and runs `npm install` so `node_modules` matches the recorded registry versions.
- Use `webstir install --dry-run` to inspect upcoming changes, or `webstir install --clean` to clear the `.webstir/` cache before reinstalling.

## Registry Notes
- Framework installs now rely on registry packages. Configure `.npmrc` with the appropriate registry URL and authentication token (currently `GH_PACKAGES_TOKEN` for GitHub Packages).
- For Sandbox or CI scenarios, provision the token and `.npmrc` before executing `framework packages publish` or `webstir install`.

## Verify Changes
- Run `./utilities/format-build.sh` to ensure formatting passes, the solution builds, and frontend tests succeed.
- When package contents change, run `framework packages sync` followed by `framework packages verify`. Run `framework packages publish --dry-run` for an end-to-end preview of the release pipeline without modifying artifacts.
- In a throwaway workspace, run `webstir install --clean` to confirm the packages reinstall from the registry and that `npm install` finishes without manual intervention.
- Optionally run `framework packages publish` in a dry environment to ensure credentials are configured before the release workflow executes.
