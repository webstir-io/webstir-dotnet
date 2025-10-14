# Framework Package Release Automation Plan

## Objectives
- Ship a single, intuitive CLI command that bumps versions, rebuilds tarballs, and publishes only the packages that changed—no extra shell/Node scripts required.
- Eliminate redundant metadata files/scripts so `package.json` becomes the lone source of truth for package versions and registry specifiers.
- Detect affected packages automatically and keep both local and CI workflows in sync.
- Lay groundwork for future packages (e.g., `@webstir-io/webstir-backend`) and optional replacements (such as third-party testing frameworks) without extra manual wiring.

## Key Decisions
- **Single entry point:** Introduce (or extend) `framework packages` subcommands (`bump`, `sync`, `release`, `publish`) so the CLI itself handles bump → build → publish with optional flags (`--package`, `--all`, `--dry-run`).
- **Automated detection:** Determine which packages changed by inspecting git diff, manifest timestamps, or dependency graphs; default to only touching those packages.
- **Version strategy:** Derive bump level from commit metadata (labels or conventional commits). Allow manual override flags for edge cases.
- **Manifest authority:** Read/write package metadata directly in `Framework/*/package.json` (and lockfiles). No additional catalog files.
- **Shared automation:** Reuse the same release command in CI (GitHub Actions) to avoid divergence between local and hosted workflows.
- **Failure handling & interactivity:** Default to fail-fast with clear remediation messages; support an opt-in `--interactive` flag for prompt-driven local runs.
- **Release notes:** Auto-generate per-package notes grouped by conventional commit type, with a `--no-notes` escape hatch.
- **Extensibility:** Allow alternative testing frameworks via configurable package entries and CLI commands (e.g., `framework packages configure --testing <npm-package>`).

## Implementation Phases
1. **Discovery & scaffolding**
   - Audit the current release flow (shell/Node helpers plus existing CLI commands) and document responsibilities that must migrate into the unified CLI.
   - Design the unified CLI command surface and migration plan (parameters, dry-run support, UX expectations).
   - Identify package metadata access points (`Framework/Packaging`, CLI) and draft a list of shared abstractions required (metadata service, process runner, git diff service).

2. **Command foundation**
   - Implement reusable helpers inside the `framework` console: a package metadata service (reads/writes manifests/lockfiles), a process runner abstraction (for npm/dotnet), and a repository diff service that shells out to `git`.
   - Extend `framework packages` with first-class subcommands (`bump`, `sync`, `release`, `publish`) plus shared option parsing (`--package`, `--all`, `--set-version`, `--dry-run`).
   - Establish consistent logging/exit codes for the new command infrastructure.

3. **Automation logic**
   - Implement change detection using the new repository diff service (e.g., `git diff --name-only`) to pick targeted packages automatically.
   - Wire in version bump heuristics (labels or commit messages) with override flags.
   - Ensure tarball rebuilds (`npm install/build/pack`) execute only for selected packages via the process runner abstraction.
   - Track per-package release results (bumped version, tarball path, publish status) for downstream reporting.

4. **Publish integration**
   - Handle npm auth detection (`GH_PACKAGES_TOKEN`, `.npmrc`) once per run with clear remediation messages.
   - Publish each touched package conditionally; gather results into a single summary.
   - Update release notes/changelog snippets per package where possible.

5. **Tool & workflow cleanup**
   - Remove the existing shell/Node scripts as soon as their responsibilities land in the CLI—no legacy compatibility required.
   - Update `format-build.sh`, CI workflows, and documentation to reference the new CLI-only flow.
   - Provide guidance for adding new packages (manifest structure, naming conventions, build hooks).
   - Stub the backend package entry (directory, manifest, disabled flag) so the new tooling handles future packages from day one.
   - Prune or refactor tests that exercised the legacy scripts; replace them with coverage for the new CLI subcommands.

6. **Testing & rollout**
   - Add unit/integration coverage for the new helper functions and CLI command variants.
   - Run end-to-end smoke tests: single package change, multiple package change, no-op release, missing credentials.
   - Pilot the command internally before switching CI/release workflows.
   - Track remaining migration tasks (script removal PRs, doc updates) to ensure nothing lingers post-cutover.

## Deliverables
- Updated `framework` console with per-package release capabilities and automation logic baked into the CLI.
- Documentation refresh (quick start, package maintenance guides, CI instructions).
- Migration notes covering removal of old scripts and how to adopt the new command.
- Optional templates for future packages (backend) or community extensions.

## Status
- Key design decisions above resolve previous open questions; remaining work is tracked in the implementation phases.
