# Package Release Automation – Phase 1 Discovery

## Current Release Tooling
- **Console entry point** – `framework packages` is implemented in `Framework/Runner.cs` and `Framework/Commands/PackageConsoleCommand.cs`. The command supports `sync`, `publish`, `verify`, and `diff`, toggled via positional arguments and boolean switches (`--frontend`, `--test`, `--prune-webstir`). The console delegates to `Framework/Packaging/PackageBuilder`.
- **Build pipeline** – `PackageBuilder` drives the end-to-end tarball flow: runs `npm ci`, `npm run build`, and `npm pack`; renames the tarball to a deterministic pattern; copies it into `Framework/Resources/webstir`; updates `Framework/Packaging/framework-packages.json`; mirrors the dependency into `Engine/Resources/package.json`; optionally publishes to the configured registry; and cleans the package directory.
- **Version utilities** – `Framework/Scripts/bump-version.mjs` increments `package.json` and `package-lock.json` versions for the frontend and testing packages. `Framework/Scripts/assert-version.mjs` guards CI steps by asserting the manifests share an expected version. *(Both helpers were retired in Phase 5 once the CLI gained parity.)*
- **Release wrapper** – `Framework/Scripts/publish.sh` orchestrates releases: resolves repo paths, validates prerequisites (`Framework/Framework.csproj`, bump script), normalizes npm config, enforces `GH_PACKAGES_TOKEN`, bumps versions (unless `--dry-run`), then invokes `dotnet run … packages publish`. *(This helper was retired in Phase 5 in favor of the CLI command surface.)*
- **CI/local helpers** – Scripts such as `utilities/local-ci.sh` and `utilities/deploy-seed.sh` call `framework packages sync|verify|publish`. Internal docs (`Docs/how-to/framework-packages.md`, `Docs/how-to/package-synchronization.md`) bake these scripts into the documented workflow.
- **State & metadata** – `Framework/Packaging/framework-packages.json` is the canonical manifest for package names, versions, registry specifiers, and tarball metadata. `FrameworkPackageCatalog` loads the manifest at runtime for installers, while `PackageTarballManager`, `FrontendPackageInstaller`, and `TestPackageInstaller` depend on the embedded tarballs that `PackageBuilder` maintains.

## Responsibilities To Migrate Into The CLI
- **Version management** – Today the Node scripts bump versions and lockfiles, detect dry-run mode, and verify consistency. The unified CLI must read/write both manifests and lockfiles, understand bump levels, and expose preview/no-op flows.
- **Change detection** – Releases rely on manual package selection (`--frontend`, `--test`) or a `--both` default. Phase 2 automation needs a diff-aware selection mechanism (git diff, dependency graph). Discovery confirms no existing service handles this; it will need to live inside the new CLI layer.
- **Build orchestration** – `PackageBuilder` is tightly coupled to the “always rebuild both packages” workflow and to embedded tarballs. Migrated commands should reuse the existing npm build/pack logic but allow targeted rebuilds and future removal of embedded artifacts.
- **Publish safeguards** – Credential checks (`GH_PACKAGES_TOKEN`, `.npmrc` resolution) and registry validation (`npm ping`) currently sit in `publish.sh` and `PackageBuilder.ValidatePublishAsync`. The CLI must incorporate these checks so CI and local runs behave identically without shell glue.
- **Tarball metadata updates** – `PackageBuilder` mutates the manifest and embedded assets on every run. The next iteration should treat the manifest (or replacement metadata store) as a first-class component exposed through a service rather than implicit file I/O inside build helpers.
- **Release reporting** – Logging today is split between shell scripts and C#. The future `framework packages release` command should emit structured summaries (bumped versions, tarball paths, publish outcomes, release notes) without relying on external scripts.

## Proposed Command Surface
### Subcommands
- `framework packages bump` – Calculates the next version(s) using commit metadata or explicit `--bump <level>` flags, updates manifests/lockfiles, and supports `--dry-run` plus `--set-version`.
- `framework packages sync` – Rebuilds tarballs for detected or specified packages. Adds `--package <name>` (repeatable), `--all`, `--changed-only` (default), and `--no-build` (metadata refresh). Accepts `--prune-webstir` until embedded tarballs disappear.
- `framework packages release` – Orchestrates bump → build → notes without publishing. Outputs a per-package summary and writes release artifacts (notes, changelog fragments) unless `--no-notes`.
- `framework packages publish` – Extends `release` by publishing artifacts. Honors `--dry-run`, `--interactive`, and `--retry` flags; collects publish results for CI consumption.
- Compatibility shims (`sync`, `verify`, `diff`) remain during migration but forward into the new pipeline with deprecation warnings.

### Shared Options & UX
- Core filtering: `--package`, `--exclude`, `--all`, `--changed-only` (default), `--since <ref>` for diff scopes.
- Version control: `--bump <patch|minor|major|auto>`, `--set-version <x.y.z>`, `--notes <path>` (seed release notes), `--no-notes`, `--notes-format <markdown|json>`.
- Safety: `--dry-run` (no file writes or publishes), `--interactive` (prompt before bump/publish steps), `--force` (override CI guards), `--verbose`.
- Exit codes align across subcommands (0 success, 1 validation failure, >1 unexpected error) so CI can gate on consistent results.

## Shared Abstractions & Touchpoints
- **Package metadata service** – Wraps manifest access (`Framework/Packaging/framework-packages.json`), package.json/lockfile reads, and future replacements. Provides typed models for name, version, registry specifier, tarball paths.
- **Git diff service** – Reads changed files (via `git diff --name-only` or libgit) to detect affected packages and determine bump levels from commit metadata or labels.
- **Process runner abstraction** – Centralizes external command execution (`npm`, `dotnet`), capturing stdout/stderr, handling timeouts, and emitting structured logs. Replaces ad-hoc `ProcessStartInfo` usage inside `PackageBuilder`.
- **Tarball/build pipeline** – Extracts rebuild logic from `PackageBuilder` into reusable helpers so bump/release/publish stages call the same code path with explicit inputs.
- **Credential & registry manager** – Normalizes npm config resolution, token detection, and registry validation so both local runs and GitHub Actions share identical pre-flight checks.
- **Release notes generator** – Reads commit history, groups changes by type, and emits package-scoped notes. Stores output in a predictable location for CI artifacts.

## Assumptions & Open Questions
- Git metadata (labels, conventional commits) will be available locally and in CI; we should confirm how the future release workflow exposes this context.
- The plan expects embedded tarballs to remain during the migration. If Phase 2 removes them, the `--prune-webstir` path becomes a no-op and should eventually be deleted.
- Publishing still targets GitHub Packages until a broader registry migration plan lands; the new tooling must keep registry URLs configurable via environment variables or CLI flags.

## Discovery Decisions
- **Version bump hints** – Use conventional commit prefixes (feat/fix/breaking) as the primary signal. They are available both locally and in CI without GitHub API calls; manual overrides can layer on via a future label or flag.
- **Embedded tarballs** – Keep the existing embedded tarball flow through Phase 2. This preserves offline installs and buys time to migrate installers once the new automation stabilizes; treat `--prune-webstir` as required legacy support for now.
- **Registry overrides** – Continue to default to GitHub Packages but read overrides from environment variables (e.g., `WEBSTIR_PACKAGE_REGISTRY`, `WEBSTIR_PACKAGE_ACCESS`). CLI flags should take precedence to support ad-hoc local runs.
