# Toolchain Packaging Refinement Plan

## Goals
- Make the framework package lifecycle obvious: source changes lead to rebuilt tarballs, regenerated manifests, and reproducible installs without guesswork.
- Reduce the number of manual steps (environment variables, script selection, `npm install` reruns) required to publish or consume an updated toolchain.
- Deliver a single command or workflow that keeps `Engine/Resources/tools`, `framework/out`, and consuming workspaces in sync.
- Support both offline archives and registry-hosted packages without diverging code paths.
- Provide clear guardrails so contributors know when to regenerate artifacts and how to verify the results.

## Current Pain Points
- Legacy tooling required multiple bash scripts (`build-frontend-package.sh`, `build-test-package.sh`) to run in the right order; this has been replaced by the managed `webstir toolchain` workflow, but we should continue simplifying configuration so no shell fallbacks resurface.
- `framework/out/manifest.json` is generated but committed, leaving contributors unsure when to rerun the scripts versus editing by hand.
- Runtime installers rewrite `package.json` and delete `node_modules` entries, which surprises anyone experimenting with alternative package managers or workspaces.
- Documentation for the workflow is spread across scripts and CLI output, making the end-to-end story feel clunky.

## Desired Experience
1. A contributor updates TypeScript sources.
2. They run a single toolchain command (e.g., `webstir toolchain sync`), which:
   - Builds the packages that changed.
   - Regenerates `framework/out/manifest.json` and embeds the manifests/resources into the CLI.
   - Records a concise changelog or diff so reviewers see what changed.
3. CI validates that the manifest matches the current tarballs and fails if artifacts are stale.
4. Consumers run `webstir install` (or a unified workflow) and reliably end up with the correct packages, whether they pull from local tarballs or a registry.

## Proposed Improvements

### 1. Unified Toolchain Command
- Introduce a CLI workflow (`toolchain sync`) that wraps the existing build scripts.
- Detect which package directories changed (git diff, timestamps) to avoid redundant builds.
- Allow explicit flags (`--frontend`, `--test`, `--all`) but default to “build everything needed.”
- Log a summary of updated versions, hashes, and whether registry specifiers were applied.

### 2. Manifest Stewardship
- Generate `framework/out/manifest.json` exclusively through code; add a header comment or metadata flag indicating the producing script and timestamp.
- Add a CI check (dotnet test, bash script, or simple Node command) that rebuilds the manifest and fails if git has diffs afterward.
- Document that contributors must never hand-edit the manifest; instead, they rerun the toolchain command.

### 3. Installer Predictability
- Evaluate reducing destructive behavior: prefer `npm install --prefer-offline` with `npm ci` fallback instead of deleting `node_modules/@electric-coding-llc/*` outright.
- Surface a dry-run mode that reports which packages would change without touching `package.json`.
- Investigate supporting alternate package managers or explicitly stating the npm requirement in docs.

### 4. Registry & Offline Parity
- Normalize configuration so the same manifest entry contains both `tarballPath` and optional `registrySpecifier`.
- Provide a single environment variable (`WEBSTIR_PACKAGE_SOURCE=auto|registry|local`) with clear precedence and doc examples.
- Ensure `toolchain sync` can optionally publish to a development registry (wrapping `npm-publisher.sh`) when requested.
- Migrate the default registry target from the local Verdaccio instance to GitHub Packages (`npm.pkg.github.com`), including PAT-based auth guidance and `.npmrc` templates for contributors and CI.

### 5. Developer Guidance
- Expand the how-to guide with a lifecycle diagram illustrating source → tarball → manifest → install.
- Add troubleshooting steps: “manifest mismatch,” “hash mismatch,” “registry auth failure,” with commands to resolve each case.
- Update onboarding docs to reference the new workflow so first-time contributors know the happy path.

## Implementation Steps
1. ✅ Introduce a `toolchain sync` CLI workflow that wraps the current build scripts (baseline experience).
2. ✅ Refactor the shell scripts into reusable modules (CLI now drives a cross-platform builder; legacy scripts delegate).
3. Implement manifest regeneration with metadata and integrate the CI/verification check (GitHub Actions running `webstir toolchain sync --verify`).
4. Update installers to support dry-run reporting and less invasive installs; document npm expectations.
5. Add registry configuration helpers, migrate the default registry to GitHub Packages, and integrate optional publishing flows.
6. Refresh documentation (how-to guide, CLI reference, troubleshooting section).
7. Monitor adoption and iterate on developer feedback; tighten CI if manual steps disappear.

## Open Questions (Working Answers)
- **Command Host** — Keep the toolchain workflow inside the existing dotnet CLI. This preserves the single binary entry point, lets us reuse dependency injection/logging, and avoids shipping an additional Node wrapper. The CLI will orchestrate shell/Node helpers as child processes when needed.
- **Manifest Versioning** — Maintain per-package semantic versions (as today) and add a generated metadata block with the command timestamp and git commit. No extra top-level manifest version is required; reviews can rely on the individual package bumps plus the generated metadata to confirm freshness.
- **Build Caching** — Detect changes via git diff of `framework/frontend` or `framework/testing`. If no tracked files changed, skip `npm pack`; otherwise rebuild and overwrite the prior tarball. Long term we can hash source folders to double-check, but git diff gating is the first milestone.
- **CI Verification** — Add a lightweight job (or test target) that runs `webstir toolchain sync --verify` in a clean checkout and fails if git status is dirty afterward. This keeps runtime under control while guaranteeing that committed manifests and tarballs match the sources.

## Success Criteria
- Contributors routinely use a single command to refresh packages; ad-hoc script usage drops.
- `webstir install` no longer surprises users with destructive operations and clearly reports what changed.
- Docs and CI guardrails catch stale artifacts before they land on `main`.
- Registry toggles (`local` vs. `registry`) become a configuration detail rather than divergent workflows.
