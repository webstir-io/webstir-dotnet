# Build Framework Packages

This guide explains how maintainers rebuild the frontend and testing packages that ship with Webstir.

## Overview
- `Framework/Frontend` and `Framework/Testing` contain the sources for the bundled `@webstir-io/webstir-frontend` and `@webstir-io/webstir-test` packages.
- The standalone `framework` console rebuilds those packages, captures tarball metadata in `Framework/Packaging/framework-packages.json`, and copies the generated archives into `Framework/Resources/webstir` so the CLI ships them as embedded resources. `Engine/Resources/package.json` points at the `.webstir` tarballs to keep new workspaces in lockstep.
- `webstir install` copies the embedded tarballs into `<workspace>/.webstir/` and pins dependencies to `file:` specifiers by default, falling back to the registry only when explicitly requested.

## Update The Packages
1. **Bump versions** – The release workflow runs `node ./Framework/Scripts/bump-version.mjs --bump <patch|minor|major>` automatically. If you want to bump manually, run the script directly (add `--dry-run` to preview without touching files).
2. **Optional diff** – Run `dotnet run --project Framework/Framework.csproj -- packages diff` to compare freshly packed tarballs with the recorded metadata without modifying files. A non-zero exit code indicates a difference.
3. **Rebuild packages** – Run `dotnet run --project Framework/Framework.csproj -- packages sync` from the repo root (or invoke the published `framework` binary).
   - Add `--frontend` or `--test` to rebuild a single package.
   - The command runs `npm ci`, `npm run build`, and `npm pack` in each package directory, then copies the tarballs into `Framework/Resources/webstir` and records metadata (path, hash, size) in `Framework/Packaging/framework-packages.json` while updating `Engine/Resources/package.json` to reference the `.webstir` archives.
   - Append `--prune-webstir` to delete cached `.webstir/*.tgz` files under `Tests/out/**` and `CLI/out/**` so local sandboxes pick up the fresh tarballs on the next run.
   - Set `WEBSTIR_FRONTEND_REGISTRY_SPEC` or `WEBSTIR_TEST_REGISTRY_SPEC` before running if you need to override the default `<name>@<version>` specifier (for example, to target a dist-tag).
4. **Verify tarballs** – Run `dotnet run --project Framework/Framework.csproj -- packages verify` to confirm the catalog metadata, repository tarballs, embedded resources, and workspace template dependencies stay in sync.
5. **Publish packages** – Run `./Framework/Scripts/publish.sh` (optionally add `--bump minor|major`; default is patch) to bump versions and push any missing releases to the configured registry (GitHub Packages by default). Append `--dry-run` to preview the next version without publishing; the helper checks `GH_PACKAGES_TOKEN` and, if present, points `NPM_CONFIG_USERCONFIG` at the repo’s `.npmrc` automatically.
6. **Commit artifacts** – Include the updated package sources, lockfiles, tarballs (`Framework/Frontend/*.tgz`, `Framework/Testing/*.tgz`, `Framework/Resources/webstir/*.tgz`), `Framework/Packaging/framework-packages.json`, and `Engine/Resources/package.json` in your PR.

## Automate Releases
- Trigger the **Release** workflow in GitHub Actions when you are ready to publish a new build.
- Choose the bump type (patch, minor, or major). With the default `auto` selection, apply a `release:{patch|minor|major}` label to the PR; the workflow picks the highest bump present.
- The workflow:
  1. Bumps both package manifests and lockfiles.
  2. Runs `dotnet run --project Framework/Framework.csproj -- packages publish` to rebuild and publish any missing versions.
  3. Commits the updated metadata back to `main`, tags `vX.Y.Z`, and creates the corresponding GitHub release (no tarball attachments required).
- Provide `dry_run: true` if you only want to preview the next version without publishing or pushing changes.

## Developer Helpers
- Run `dotnet run --project Framework/Framework.csproj -- packages diff` to check whether your changes would alter the packaged tarballs before touching tracked files.
- Before committing frontend/testing changes, run `dotnet run --project Framework/Framework.csproj -- packages sync` (optionally `--prune-webstir`) followed by `dotnet run --project Framework/Framework.csproj -- packages verify` to ensure local artifacts, metadata, and embedded tarballs are in sync.
- Use `node ./Framework/Scripts/bump-version.mjs --bump patch --dry-run` to preview the next version without touching files.

## Install In A Workspace
- Run `webstir install` (or any workflow that calls it) inside a consuming workspace.
- The installer copies the embedded tarballs into `<workspace>/.webstir/`, pins dependencies to `file:./.webstir/<tarball>.tgz`, and reruns `npm install` when those archives change. If a tarball is missing or corrupt, it falls back to the registry with a warning.
- Use `webstir install --dry-run` to inspect upcoming changes, or `webstir install --clean` to delete cached tarballs before reinstalling.

## Registry Notes
- Tarball installs do not require registry access. Authentication is only needed for publishing or when you explicitly opt into registry installs.
- During the private phase, keep `.npmrc` pointed at GitHub Packages and ensure `GH_PACKAGES_TOKEN` is available before running `framework packages publish` (or manual registry fallbacks). When ready to publish publicly, update the registry URL and remove the token requirement.
- For Sandbox or CI scenarios that need registry access, create a `.npmrc` with the GitHub Packages token (see `Sandbox/README.md`) before executing publish or registry-fallback commands.

## Verify Changes
- Run `./utilities/format-build.sh` to ensure formatting passes, the solution builds, and frontend tests succeed.
- When package contents change, run `framework packages sync` followed by `framework packages verify` (or use `./Framework/Scripts/publish.sh` which wraps those steps during a publish).
- In a throwaway workspace, run `webstir install --clean` to confirm the new tarballs resolve correctly and that `npm install` finishes without manual intervention.
- Optionally run `framework packages publish` in a dry environment to ensure credentials are configured before the release workflow executes.
