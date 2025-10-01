# Framework Tarball Distribution Plan

## At a Glance
- Run `framework packages diff` to preview changes, then `framework packages sync && framework packages verify` after modifying `framework/frontend` or `framework/testing`; commit the refreshed tarballs and metadata.
- Use `./utilities/format-build.sh` to regenerate tarballs, verify hashes, and ensure the solution builds before pushing.
- Let `webstir install` (and other workflows) manage `.webstir` tarballs automatically; exports like `WEBSTIR_PACKAGE_SOURCE=registry` are only needed when explicitly testing the registry path.
- When preparing a release, run `framework packages publish` from CI so the same tarballs reach GitHub Packages.

## Background
- Registry-first installs introduce long feedback loops in CI and local workflows, especially when credentials are missing or throttled.
- We previously removed committed tarballs to slim the repo, but the registry dependency made tests unreliable and harder to run offline.
- A refreshed "tarball-first" flow should release friction, stay in sync automatically, and still let us publish to GitHub Packages when needed.

## Goals
- Ship reproducible framework packages (`@electric-coding-llc/webstir-frontend`, `@electric-coding-llc/webstir-test`) without requiring npm auth or network access during tests.
- Distribute tarballs with the CLI/runtime so every install has the artifacts it needs on day one.
- Regenerate tarballs mechanically (`framework packages sync`) and fail fast if the committed archives drift from source.
- Keep the registry path available behind an opt-in flag so we can smoke-test publishing and eventually migrate back if desired.
- Guarantee that a fresh checkout + `./utilities/format-build.sh` leaves every workspace ready to run `webstir test` immediately.

## Non-Goals
- Supporting bespoke per-workspace overrides; the CLI will always favor the bundled tarballs unless explicitly told otherwise.
- Changing published package contents or versioning. This plan only affects how we distribute and consume the artifacts.
- Reintroducing large binary blobs checked directly into the repo root—tarballs stay scoped under the package directories or embedded resources.

## Developer Experience
- No setup required: the repo includes the current tarballs (tracked in git) and the shipped `webstir` binary embeds the same assets so global installs behave identically. Running `webstir install` or any workflow that depends on it pulls from `.webstir/<package>.tgz` automatically.
- Clear status messages explain when tarballs are copied, validated, or invalid—"run `framework packages sync`" is the default remediation.
- Environment variable `WEBSTIR_PACKAGE_SOURCE` continues to gate behavior:
  - `tarball` (default) → prefer local archives.
  - `registry` or `npm` → install from the configured registry, using the spec recorded in the package catalog.
- `framework packages sync` prints a diff summary (versions, tarball hashes) so reviewers can verify the change without unpacking anything.

## Workspace Asset Directory
- Rename the workspace asset folder from `.tools` to `.webstir` to make clear the contents are essential runtime assets.
- Update `Engine/Constants`, `AppWorkspace`, TypeScript helpers, scripts, and tests to use the new directory name. Because the tarball workflow debuts alongside this rename, we do not need backwards-compat migration code.
- Reserve `.webstir` for framework-managed artifacts (tarballs, manifests, logs) so the workspace root stays tidy and predictable.

## Packaging Workflow
1. `framework packages sync`
   - Runs `npm ci`, `npm run build`, and `npm pack` inside each package directory.
   - Writes tarballs to a deterministic name (`webstir-test-<version>.tgz`, etc.), stages them for commit, and records metadata (checksum, size, relative path) in `framework/Packaging/framework-packages.json`.
   - Updates `Engine/Resources/package.json` to keep dependency versions aligned.
   - Exits non-zero if the regenerated checksum differs from the committed metadata or if the tarball directory is dirty after the command finishes, guarding against stale bundles and missing commits.
   - Optional: run `framework packages diff` first to see which tarballs would change without modifying files.
2. `framework packages publish`
   - Uses the same tarballs to push to GitHub Packages when `GH_PACKAGES_TOKEN` is present.
   - Skips publish gracefully when the token is missing or the version already exists.

## Installer Behavior
- `FrontendPackageInstaller` and `TestPackageInstaller` load the tarball metadata, copy the archive into `<workspace>/.webstir/`, validate its SHA256, and pin the dependency via `file:.webstir/<package>.tgz`.
- Before copying, the installer removes any existing tarballs that match the package prefix (for example, `webstir-frontend-*.tgz`) so only the current version remains in `.webstir`.
- If the tarball is missing or fails checksum validation, the CLI reruns `framework packages sync` (or instructs the user to do so) before falling back to the registry path.
- Install state messages highlight whether a reinstall happened, whether a registry fallback was required, and what remediation step the developer should take.

## CI / Automation
- `ci.yml`
  - Still installs Node modules to build the source packages, but tarball-based tests no longer require registry access.
  - After `framework packages sync`, add a `framework packages verify` (new command) that checks metadata + tarball hashes.
  - Remove `packages: read` permission once registry calls are optional.
- `release.yml`
  - Keeps the publish step (needs `packages: write`).
  - Commits tarballs + metadata updates automatically as part of the release PR.
- Local tooling (`./utilities/format-build.sh`)
  - Already runs `framework packages sync`; extend it to run the verify step so contributors catch drift before sending a PR.

## Testing Strategy
- Extend `Tests/PackageInstallers` to cover:
  - Tarball pinning (`file:` spec in `package.json`).
  - Checksum mismatch detection (corrupted `.webstir` file triggers a resync message).
  - Registry fallback path via `WEBSTIR_PACKAGE_SOURCE=registry`.
- Add a smoke test that seeds a workspace, deletes `.npmrc`, and ensures `webstir test` still passes—proves the no-setup promise.

## Risks & Mitigations
- **Large Git diffs**: Tarballs can add binary size to commits. Mitigate by keeping packages small, ensuring deterministic rebuilds, and reviewing tarball diffs via the `framework packages diff` helper.
- **Human error updating only metadata**: The verify command fails if the tarball contents and metadata disagree.
- **Stale caches**: The installer always recomputes checksums before reuse; failed validation forces a rebuild instead of silently reusing bad data.

## Cache Policy
- Tarball file names always include the package version; installers purge older matching archives before copying the current tarball into `.webstir`.
- `framework packages sync` can optionally run a `--prune-webstir` mode that wipes `<workspace>/.webstir/*.tgz` when preparing a workspace for tests, ensuring developer sandboxes never carry forward obsolete bundles.
- Developers who need a clean slate can run `webstir install --clean`, which deletes `.webstir` tarballs and reinstalls from the bundled archives.

## Versioning Strategy
- Default to manual version bumps triggered by intentional package changes rather than automatic semver increments.
- Provide a `framework packages diff` command (or extend `sync`) that surfaces source and tarball changes to guide the appropriate bump level.
- Only run `framework packages publish` from the release workflow and when a new version is ready to push; everyday CI and developers rely solely on local tarballs.

## Public Release Outlook
- Even after moving to npmjs.org, keep tarball-first as the default to preserve deterministic installs and fast CI. The registry path remains available behind `WEBSTIR_PACKAGE_SOURCE=registry` for smoke testing and publisher validation.

## Implementation Steps
1. **Workspace directory rename** – Switch all constants, helpers, scripts, and tests from `.tools` to `.webstir`.
2. **Package build pipeline** – Update `PackageBuilder` to emit tarball descriptors (path, checksum, size), stage regenerated archives, and add `framework packages verify` / `framework packages diff` hooks that fail when metadata and tarballs drift.
3. **Catalog & resources** – Extend `FrameworkPackageCatalog` (and `Framework.csproj`) so tarball metadata is available at runtime, and ensure the published `webstir` binary embeds or copies the same artifacts used in the repo.
4. **Installer flow** – Introduce a shared tarball manager that clears stale `.webstir` archives, copies the bundled tarball, validates its checksum, and only falls back to the registry when explicitly requested or recovery fails.
5. **CLI ergonomics** – Add support for `framework packages sync --prune-webstir` and `webstir install --clean`, wiring status messages so users understand when tarballs were refreshed or pruned.
6. **Tooling & CI** – Update `./utilities/format-build.sh`, `ci.yml`, and `release.yml` to run the new verify step, drop unnecessary registry permissions, and publish using the bundled tarballs.
7. **Documentation & onboarding** – Refresh `docs/how-to/package-synchronization.md`, CLI help, and onboarding guides to reference this tarball-first workflow and call out the registry fallback flag.
