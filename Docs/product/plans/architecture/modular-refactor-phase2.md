# Modular Refactor Phase 2 — Package Externalization Playbook

## Objectives
- Decouple framework packages (`@webstir-io/webstir-frontend`, `@webstir-io/webstir-backend`, `@webstir-io/webstir-test`, contracts) from the mono-repo so they can ship independently.
- Replace tarball-based distribution with registry-first publishing and automated provenance.
- Ensure the Webstir CLI consumes registry versions through deterministic tooling (pinning, workspace sync, contract validation).

## Target State
- Each package lives in its own repository with CI/CD covering lint, tests, build, npm publish, SBOM/provenance, and release notes.
- The main repo references published versions (semver ranges) via package.json and uses workspace tooling only for development.
- Publishing flows (`framework packages publish`) orchestrate releases by invoking the external repos’ pipelines or by consuming released artifacts—no tarballs checked into Webstir.
- Contract packages (`@webstir-io/testing-contract`, `@webstir-io/module-contract`) provide the shared reference types; other repos depend on them via the registry.

## Work Breakdown

### 1. Repository Bootstrapping
- **Create new Git repositories** for each package (frontend, backend, testing, contracts). Include:
  - MIT license, README, CHANGELOG template, CONTRIBUTING.
  - .editorconfig, lint rules, TypeScript configuration, build scripts mirroring current setup.
  - GitHub Actions (build/test, release, provenance), CODEOWNERS, issue templates.
- **Preserve history** by using `git subtree split` or `git filter-repo` so existing commits migrate cleanly.
- **Set up publishing credentials** (GitHub Packages PAT, later npmjs token) via repository secrets. Document environment variables and required roles.

### 2. Package Build & Publish Automation
- Author `release.yml` workflow for each repo:
  - Validate `npm run build`, run contract validation (`npm run validate:contracts` for testing-contract).
  - Run unit/integration tests (existing Node suites).
  - Publish to GitHub Packages with provenance; skip if version already exists.
  - Attach changelog/metadata to GitHub release.
- Optional: create reusable workflow templates to keep repos in sync.
- Add prepublish checks (lint, test, schema validation) to package scripts to guard manual publishes.

### 3. Mono-Repo Integration
- Update Webstir’s `package.json` dependencies to use semver ranges (e.g., `"@webstir-io/webstir-test": "^0.1.0"`), removing `file:` references.
- Add tooling to pin versions during CI: extend `framework packages sync` to read a lock manifest (e.g., `Framework/Packaging/framework-packages.json`) and write dependency updates automatically.
- Enhance `utilities/format-build.sh` (already validating contracts) to also run `npm run build:contracts` when working against unpublished workspaces (script now available at repo root).
- Provide scripts to link local packages during development (`npm install <tarball>` or `npm link`). Document the workflow in `Docs/how-to/framework-packages.md`.

### 4. Installer & CLI Adjustments
- Refactor installers (`FrontendPackageInstaller`, `BackendPackageInstaller`, `TestPackageInstaller`) to:
  - Read desired package spec from configuration (default to pinned version).
  - Execute `npm install` directly against registry specifiers.
  - Detect mismatches by inspecting `package.json` and `package-lock.json` rather than tarball hashes.
- Update `PackageSynchronizer` to track installed version vs. desired spec and trigger reinstalls accordingly.
- Extend diagnostic messages to explain registry failures (missing token, 404, etc.).

### 5. Workflow & Script Cleanup
- Remove tarball generation, manifest writers, and `Framework/out/**` artifacts from the repo and build steps.
- Update CI scripts (`utilities/local-ci.sh`, `.github/workflows/release.yml`) to rely on published packages. For release, fetch versions from registry instead of packaging tarballs.
- Add schema validation (already available) and version checks to the release workflow (`framework packages publish` should confirm new versions are staged before triggering external repos).

### 6. Documentation & Enablement
- Refresh:
  - `Docs/how-to/framework-packages.md`
  - `Docs/how-to/package-synchronization.md`
  - `Docs/reference/cli.md` (test/install flows)
- Include guidance on:
  - Configuring `.npmrc` and tokens.
  - Developing against local package clones (using npm workspaces or `npm link`).
  - Release steps (bumping versions, changelog updates).
- Publish an FAQ covering error scenarios (auth failure, incompatible versions, rollback strategy).

### 7. Validation & Rollout
- Create integration tests that mock registry installs (e.g., using Verdaccio or `npm pack` + temporary registry) to ensure installers behave as expected.
- Dry-run release: publish prerelease versions of packages, point Webstir to them via feature branch, and run full `webstir init/install/build/test`.
- Plan communication for the breaking change (requires registry access) and schedule migration window.

## Deliverables
- External repositories live and publishing successfully.
- Webstir mono-repo consuming registry versions with automated validation and no committed tarballs.
- Updated documentation and scripts reflecting the new flow.
- Release checklist for future updates (who triggers which pipeline, how versions propagate).

## Open Questions
- How to manage synchronized version bumps across multiple packages (consider changesets or release orchestration tooling)?
- Do we need fallbacks for offline/local installs (e.g., allow customers to supply tarball path)?
- How will we track provenance/SLSA compliance for registry publishes?
- Should we maintain a meta “framework” package that pins a known-good matrix of versions?

## Suggested Timeline
1. Week 1–2: repository bootstrap + workflows (prerelease builds).
2. Week 3: mono-repo dependency updates, installer refactor, contract validation integration.
3. Week 4: docs refresh, integration tests, dry-run release.
4. Week 5: GA release, monitor adoption, backlog cleanup.
