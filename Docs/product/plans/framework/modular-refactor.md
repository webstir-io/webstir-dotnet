# Modular Refactor Plan

## Context
- Contract packages (`@webstir-io/testing-contract`, `@webstir-io/module-contract`) are already published with generated schemas.
- Framework tooling (`@webstir-io/webstir-testing`, `@webstir-io/webstir-frontend`, `@webstir-io/webstir-backend`, etc.) lives in dedicated repositories and publishes to GitHub Packages; the mono-repo consumes pinned registry versions.
- Provider overrides are supported through environment variables and `webstir.providers.json`.
- Solo maintainer project: no external teams, staged rollouts, or legacy migration paths to support.

## Objectives
- Finish the host/provider split inside Webstir so testing follows the same provider model as the frontend and backend hosts.
- Deliver at least one alternate provider (Vitest) to prove the contract in practice.
- Keep CLI help, documentation, and automation aligned with the modularised architecture.

## Completed Foundations
- Framework packages and contract definitions extracted with their own CI/release workflows.
- Registry-first install flow verified; `framework packages sync/publish` pins versions without tarballs.
- Provider selection knobs exposed via env/config with defaults pointing to the VM-based provider.

## Completed Work
- Webstir test host now owns CLI/manifest/event orchestration; default provider lives in `@webstir-io/webstir-testing`.
- Vitest provider promoted to its own repository (`@webstir-io/vitest-testing`) with CI/release automation and documentation updates across Webstir to cover provider selection/registry installs.
- Vite frontend provider pulled into `@webstir-io/vite-frontend` with matching automation; guidance updated to reference standalone packages instead of in-repo pilots.

## Remaining Work

### 1. Refresh CLI & Docs
- Ensure scaffolding commands and help output clearly surface provider choices or at least call out the override knobs.
- Audit remaining docs/scripts for tarball-era references and remove them, adding Vitest/Vite quickstarts where helpful.
- Document configuration guidance (timeouts, watch mode, known gaps) for the optional providers.

### 2. Harden Runtime & Automation
- Wrap up TODOs in the VM runtime (e.g., snapshot support, parallelism toggles) or document intentional gaps.
- Confirm `framework packages sync/publish` relies solely on registry installs, adding clear logging for auth or registry failures.  
  - ✅ Default registry install path now logs GH_PACKAGES_TOKEN guidance on failure.
- (Deferred) Extend CI to run host + provider suites (matrix covering VM + Vitest/Vite) once alternate providers are back in scope.

### 3. Repository Hygiene
- Delete leftover scripts/configuration from the tarball workflow and prune redundant assets.  
  - ✅ Removed legacy backend tarball asset (`Framework/Backend/webstir-backend-0-0-0.tgz`) and archived the tarball-era plan.
- Update `CONTRIBUTING.md` to explain the simplified dev/publish flow.  
  - ✅ Contributing guide now documents registry auth, local workflows, and release steps.
- Keep docs aligned with the new package layout (no references to retired ADRs or sub-phase files).  
  - ✅ Installation plan updated for registry installs; tarball plans marked as historical context-only.
- ✅ Default testing doc now captures the remaining VM runtime limitations (snapshots/parallelism) so the TODOs are documented until features land.

## Validation Checklist
- `dotnet test Tester/Tester.csproj` succeeds with default and Vitest providers.
- `webstir test` on a clean workspace installs packages from the registry only (requires `GH_PACKAGES_TOKEN`; CLI now surfaces auth guidance when creds are missing).
- CLI help and docs reference the host/provider setup and point to live packages.
- GitHub Actions execute the default provider suite on every push (alternate provider matrix deferred).

## Immediate Next Actions
Plan complete — defer alternate provider matrix until those providers re-enter scope.
