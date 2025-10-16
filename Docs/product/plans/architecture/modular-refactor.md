# Modular Refactor Roadmap

## Motivation
- Decouple framework packaging, testing, and release flows so teams can evolve each piece independently.
- Formalize the adapter contract (`discoverTestManifest` → `WEBSTIR_TEST …` events) before multiple runtimes rely on it.
- Enable external contributors and partner teams to extend Webstir without cloning the monorepo or carrying bespoke tarballs.
- Reduce operational risk by shrinking cross-project blast radius for releases and infrastructure changes.
- Allow frontend and backend modules to be swapped or extended without altering the CLI core.

## Goals
- Design a pluggable runtime architecture that keeps the current CLI contract but permits alternate test providers.
- Break `@webstir-io/webstir-frontend` and `@webstir-io/webstir-test` into standalone repositories with automated publishing.
- Define a module-provider interface so alternative frontend/backend stacks can plug in behind stable contracts.
- Refresh packaging, installation, and CI workflows to consume registry artifacts exclusively.
- Document a durable versioning, support, and migration policy that future teams can follow.

## Non-Goals
- Rewriting the actual test assertion DSL beyond gaps identified in Phase 0.
- Shipping public npm access or license changes (tracked separately).
- Guaranteeing offline installs once registry-first distribution is in place.

## Guiding Principles
- **Compatibility first**: keep the event stream and CLI surface stable; gate breaking changes behind semver and clear rollout plans.
- **Composable architecture**: treat runtimes as providers behind a small interface so other ecosystems can plug in.
- **Incremental delivery**: land each phase with feature flags or shims that keep the existing automation green.
- **Observable operations**: provide metrics and logging hooks so new repos and adapters are easy to diagnose in CI.

## Current State Snapshot
- Framework packages live inside the mono-repo, rely on committed tarballs, and ship via `framework packages sync/publish`.
- The custom `webstir-testing` runtime owns manifest discovery, execution, and assertion logic in one package.
- `.NET` workflows (`Engine/Bridge/Test/TestCliRunner.cs`) parse `WEBSTIR_TEST` JSON events but cannot swap executors.
- Release workflows assume local tarballs exist and require manual coordination to produce distro assets.

## Target State Overview
- Adapter host package (`@webstir-io/webstir-test-host`) responsible for CLI, manifest handling, and event emission; delegates to provider implementations.
- Provider interface published as TypeScript types and docs so alternates (e.g., Vitest, Playwright) can emit Webstir-compliant summaries.
- Standalone GitHub repos for each framework package with their own CI/CD (build, test, npm publish, provenance attestation).
- Webstir CLI consumes versioned registry packages; format/build scripts verify compatibility via contract tests.
- Module providers (frontend, backend) implement published interfaces describing build inputs/outputs, lifecycle hooks, and manifest metadata; defaults live in official repos and third parties can publish alternatives.

## Work Plan

### Phase 0 – Discovery & Contract
- **Scope**: capture every guarantee the current runtime provides and the gaps blocking wider adoption.
- **Key Tasks**
  - Audit manifest/event shapes; extract into a spec with examples and schema references.
  - Catalog runtime features (assertions, watch mode, async handling) and quality issues to fix before opening the surface.
  - Draft an Architectural Decision Record (ADR) capturing the adapter contract, compatibility rules, and versioning policy.
- **Deliverables**: Published ADR, JSON schema (or TypeScript definitions) for manifest and event payloads, prioritized backlog of runtime enhancements.
- **Exit Criteria**: Core team signs off on the contract; future phases can implement against the documented surface without guessing.

### Phase 1 – Adapter Architecture
- **Scope**: restructure the existing runtime into host + provider layers without changing behavior.
- **Key Tasks**
  - Introduce a provider interface and re-home CLI/event code in a dedicated host module.
  - Wrap the current VM-based executor as the default provider; cover it with integration tests that replay manifest fixtures.
  - Update `Engine/Bridge/Test/TestCliRunner` (and related utilities) only as needed to align with the new package boundaries.
- **Deliverables**: Refactored package structure, automated tests validating the event stream, migration notes for repository consumers.
- **Exit Criteria**: `webstir test` behaves identically pre/post merge; all CI jobs remain green using the default provider.

### Phase 2 – Externalization & Versioning
- **Scope**: move framework packages into independent repositories and wire up automated releases.
- **Key Tasks**
  - Spin up repos with shared templates (linting, formatting, release workflows, CODEOWNERS).
  - Configure semver tagging, changelog generation, provenance artifacts, and npm publishing to GitHub Packages.
  - Replace `file:` tarball references with registry specifiers in the mono-repo; introduce tooling to pin versions for CI.
- Detailed playbook: `Docs/product/plans/architecture/modular-refactor-phase2.md`
- **Deliverables**: New repositories online, release pipelines passing, mono-repo consuming published versions, runbooks for contributors.
- **Exit Criteria**: Mono-repo builds/tests succeed without local tarballs; package releases can be triggered without editing Webstir core.

### Phase 3 – Module Plug-In Framework
- **Scope**: establish contracts and tooling that let frontend/backends swap modules independently of the CLI.
- **Key Tasks**
  - Document module provider requirements (build command, artifact layout, config schema, compatibility metadata) and publish TypeScript definitions.
  - Refactor scaffolding, install, and build workflows to resolve modules dynamically via identifiers (registry specifier, local path) and lifecycle hooks.
  - Extract default frontend/backend modules into provider packages that implement the contract; add contract tests to validate outputs.
  - Provide extension points for per-module assets (templates, static files) and register them through configuration rather than hardcoded paths.
- **Deliverables**: Module provider interface, updated workflows respecting the contract, default providers packaged independently, developer documentation for registering new modules.
- **Exit Criteria**: CLI can load modules through the provider interface; swapping to a sample alternate module works end-to-end in a seed workspace.
- Detailed playbook: `Docs/product/plans/architecture/modular-refactor-phase3.md`

### Phase 4 – Alternative Provider Pilot
- **Scope**: validate the adapter contract by implementing and trialing at least one mainstream runner (e.g., Vitest).
- **Key Tasks**
  - Build a prototype provider translating Webstir manifests into the target runner’s configuration, emitting compliant events.
  - Run pilot projects/seed workspaces through sanity suites; capture metrics around runtime, diagnostics, flake rate.
  - Identify missing hooks (e.g., timeouts, setup/teardown) and feed improvements back into the provider API.
- **Deliverables**: Reference provider, pilot report comparing default vs. alternative runtime, API adjustments (if needed).
- **Exit Criteria**: Adapter contract proven workable; decision recorded on which providers ship as supported vs. community.

### Phase 5 – Workflow & Tooling Updates
- **Scope**: propagate the new architecture through CLI commands, docs, and CI pipelines.
- **Key Tasks**
  - Update `webstir init/add/test` flows to surface provider selection and document default choices.
  - Refresh `./utilities/format-build.sh`, local scripts, and GitHub workflows to validate both host and provider contracts.
  - Rewrite guides (`Docs/how-to/test.md`, `Docs/reference/cli.md`, etc.) to explain adapter usage and provider swap steps.
- **Deliverables**: Updated tooling, documentation, and developer onboarding material.
- **Exit Criteria**: New workflows validated end-to-end; contributors can select providers with no manual file edits.

### Phase 6 – Rollout & Feedback
- **Scope**: stage the release, gather telemetry, and iterate before general availability.
- **Key Tasks**
  - Migrate internal teams incrementally; monitor crash reports, exit codes, and user feedback channels.
  - Schedule GA announcement, deprecation timelines for legacy tarball flows, and establish SLAs for provider issues.
  - Review metrics after the first full release cycle and adjust governance (e.g., release cadence, support tiers) as needed.
- **Deliverables**: Release communications, adoption dashboards, post-GA lessons learned.
- **Exit Criteria**: Stable adoption across target teams, documented support model, backlog reprioritized based on real-world feedback.

## Cross-Cutting Workstreams
- **Runtime Quality**: Expand assertion library, add snapshot support, parallel execution, and test timeouts where required by the contract.
- **Security & Compliance**: Ensure new repos inherit dependency scanning, license checks, and signing policies; document SBOM expectations.
- **Developer Experience**: Provide VS Code/JetBrains launch configs, debugging tips, and generator templates reflecting the modular architecture.
- **Documentation & Training**: Maintain living docs (how-to guides, FAQs, troubleshooting) in the main repo and mirror relevant sections in satellite repos.
- **Module Ecosystem**: Establish certification criteria, compatibility matrices, and sample modules to encourage community contributions while maintaining quality.

## Risks & Mitigations
- **Contract Drift**: Mitigate by publishing JSON schemas and adding CI contract tests that fail when payloads change unexpectedly.
- **Release Fragmentation**: Centralize release notes and cross-link repos; automate dependency update PRs back into Webstir via bots.
- **Adoption Hesitation**: Run early adopter programs and offer migration support; keep the legacy flow available behind a feature flag during transition.
- **Provider Ecosystem Debt**: Establish contribution guidelines and certification criteria so third-party providers meet quality bars before endorsement.
- **Module Compatibility Breaks**: Require semver discipline, compatibility testing, and fallback modules so workspace scaffolds remain functional if a provider misbehaves.

## Milestones & Metrics
- Phase 0 complete → ADR merged, schemas published.
- Phase 1 complete → `webstir test` default provider shipped via new host architecture.
- Phase 2 complete → External repos releasing independently; Webstir pinned to registry versions.
- Phase 3 complete → Module provider interface live; default frontend/backend modules consumed through the contract.
- Phase 4 pilot → Alternative runtime provider executing sample suites with comparable pass rates (<2% delta) and documented gaps.
- Phase 5 complete → Updated CLI/docs live; provider selection supported in new workspaces.
- Phase 6 GA → 90% of internal teams migrated; mean time to diagnose provider issues < 1 day.

## Dependencies & Coordination
- Align with packaging plan (`Docs/product/plans/framework/packaging.md`) to avoid conflicting distribution changes.
- Coordinate with Release Engineering for CI updates and artifact provenance requirements.
- Loop in Developer Experience and Docs teams for tooling changes and user-facing announcements.

## Next Steps
- Assign owners for Phase 0 tasks and schedule a kickoff review.
- Stand up a shared tracking board covering cross-repo issues and milestone burndown.
- Prepare communication plan (internal RFC, stakeholder briefings) ahead of the first breaking change window.
