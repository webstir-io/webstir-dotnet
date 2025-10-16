# ADR 0001: Webstir Adapter Contracts for Testing and Module Providers

## Status
Proposed

## Context
- The Webstir CLI currently bundles testing, frontend, and backend behavior inside the mono-repo, with implicit contracts defined by implementation details.
- Contributors cannot replace the TypeScript test runner or framework modules without editing core code, limiting experimentation and long-term support.
- Planned packaging changes move toward registry-backed distribution and independent repositories, which require stable, versioned contracts.
- Existing tooling (e.g., `Engine/Bridge/Test/TestCliRunner`) already depends on a structured event stream (`WEBSTIR_TEST …`) and manifest shape but those guarantees are undocumented.

## Decision
- Publish two contract packages:
  - `@webstir-io/testing-contract`: TypeScript definitions + JSON Schemas for the test manifest and runner event protocol.
  - `@webstir-io/module-contract`: TypeScript definitions describing frontend/backend module providers (metadata, build lifecycle, workspace resolution).
- Adopt semantic versioning for each contract package. Breaking changes require a major version bump and coordinated CLI/runtime release notes.
- Generate JSON Schemas from the TypeScript sources and embed validation in the CLI to reject non-conforming providers at load time.
- Maintain backwards-compatible default implementations (current runtime and modules) that implement these contracts.
- Track contract compatibility in CI with golden fixtures to prevent accidental drift.

## Consequences
- **Positive**
  - Enables alternate test runners or framework stacks without modifying the CLI core.
  - Allows packaging repositories to evolve independently while guaranteeing compatibility.
  - Provides a clear surface for community contributions and internal experimentation.
- **Negative**
  - Introduces maintenance overhead for contract packages and schemas.
  - Requires rigorous semver governance and version negotiation to avoid breaking consumers.
  - Adds validation logic to CLI workflows, which may initially surface configuration errors users have to resolve.

## Implementation Plan
1. Create the contract packages in the repository with TypeScript sources, build metadata, and generated `.d.ts`/schema artifacts.
2. Wire the CLI to reference the contract definitions and validate provider manifests/events at runtime.
3. Update default runtime and module packages to depend on the contract packages and demonstrate compliance.
4. Publish documentation (how-to guides, troubleshooting) describing the adapter surface and migration steps.
5. Establish CI checks:
   - Generate schemas during build and fail if definitions drift without regenerating artifacts.
   - Replay fixture events/manifests through the `.NET` bridge to ensure compatibility.
6. After validation, publish the contract packages to the registry and tag ADR 0001 as Accepted.

## Alternatives Considered
- **Inline documentation only**: rejected because prose lacks enforceable guarantees and tooling support.
- **Single combined contract package**: deferred in favor of separate testing and module packages to allow independent versioning cadences.
- **Keep implicit contracts**: rejected; would lock Webstir into bespoke implementations and block modular roadmap.

## Related Work
- `Docs/product/plans/architecture/modular-refactor.md`
- `Docs/product/plans/architecture/modular-refactor-phase0.md`
