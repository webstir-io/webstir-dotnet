# Modular Refactor Phase 0 — Discovery Packet

## Objectives
- Capture the explicit contracts the CLI already relies on for testing, packaging, and scaffolding.
- Translate those contracts into publishable TypeScript definitions / JSON schemas so the adapter surface is unambiguous.
- Outline the Architectural Decision Record (ADR) that will ratify compatibility rules, versioning policy, and governance.
- Surface the prioritized backlog of runtime and module enhancements required before opening the ecosystem.

## Current Contract Inventory

### Testing Runtime Manifest
- Produced by `discoverTestManifest` (`Framework/Testing/src/discovery.ts`).
- Scans `<workspace>/src/**/tests/**` for files ending in `.test.ts` or `.test.js`.
- Emits modules sorted lexicographically by `id`.

```ts
export interface TestManifest {
  readonly workspaceRoot: string;         // Absolute path to workspace.
  readonly generatedAt: string;           // ISO timestamp.
  readonly modules: readonly TestModule[];
}

export interface TestModule {
  readonly id: string;                    // Forward-slash relative path (no extension).
  readonly runtime: 'frontend' | 'backend';
  readonly sourcePath: string;            // Absolute path to the source test file.
  readonly compiledPath: string | null;   // Absolute path to emitted .js file under build/.
}
```

**Semantics**
- `compiledPath` points to `<workspace>/build/<relative>.js`; null indicates missing build output.
- Unknown runtimes are logged and skipped.
- Manifest contains zero modules when `src/` is absent or no matching files are found.

### Testing Runtime Events
- Runner emits newline-delimited JSON prefixed with `WEBSTIR_TEST ` (`Framework/Testing/src/events.ts`).
- `.NET` bridge consumes and aggregates these events (`Engine/Bridge/Test/TestCliRunner.cs`).

```ts
export type RunnerEvent =
  | RunnerStartEvent
  | RunnerResultEvent
  | RunnerSummaryEvent
  | RunnerLogEvent
  | RunnerErrorEvent
  | RunnerWatchIterationEvent;

export interface RunnerStartEvent {
  readonly type: 'start';
  readonly runId: string;
  readonly manifest: TestManifest;
}

export interface RunnerResultEvent {
  readonly type: 'result';
  readonly runId: string;
  readonly runtime: 'frontend' | 'backend';
  readonly moduleId: string;
  readonly result: TestRunResult;
}

export interface TestRunResult {
  readonly name: string;
  readonly file: string;                  // Absolute compiled path.
  readonly passed: boolean;
  readonly message: string | null;        // Stack or assertion text when failed.
  readonly durationMs: number;
}

export interface RunnerSummaryEvent {
  readonly type: 'summary';
  readonly runId: string;
  readonly runtime: 'frontend' | 'backend' | 'all';
  readonly summary: RunnerSummary;
}

export interface RunnerSummary {
  readonly passed: number;
  readonly failed: number;
  readonly total: number;
  readonly durationMs: number;
  readonly results: readonly TestRunResult[];
}

export interface RunnerLogEvent {
  readonly type: 'log';
  readonly runId: string;
  readonly level: 'info' | 'warn' | 'error';
  readonly message: string;
}

export interface RunnerErrorEvent {
  readonly type: 'error';
  readonly runId: string;
  readonly message: string;
  readonly stack?: string;
}

export interface RunnerWatchIterationEvent {
  readonly type: 'watch-iteration';
  readonly runId: string;
  readonly iteration: number;
  readonly phase: 'start' | 'complete';
  readonly changedFiles: readonly string[];
  readonly summary?: RunnerSummary;
}
```

**Semantics**
- Events are emitted sequentially per run; summaries for each runtime precede the final `runtime: 'all'` summary.
- Missing compiled modules yield synthetic `[missing compiled file]` results prior to test execution.
- Module evaluation errors emit `[module evaluation]` failures and skip registered tests from that file.
- Watch mode debounces changes; each iteration re-runs discovery before execution.

### Frontend / Backend Module Expectations
- Default modules live under `Framework/Frontend` and `Framework/Backend`; bundled as npm packages.
- Build outputs are staged under `<workspace>/build/frontend/**` and `<workspace>/build/backend/**`.
- Scaffolding templates and static assets are currently hard-coded via `Engine/Resources/**`.
- Install workflows (`Engine/Workflows/InitWorkflow.cs`, `InstallWorkflow.cs`) assume both modules exist and pin them via package.json entries.

**Implicit Contract**
- Module packages expose npm scripts (`build`, `clean`) and follow the `dist/` export layout consumed by `webstir`.
- CLI expects each module to supply:
  - **Scaffold templates** (copied into new workspaces).
  - **Build command** invoked during `webstir build`.
  - **Manifest entries** describing output directories used by tests/publish.
- There is no explicit interface; behavior is encoded in installer and scaffold utilities.

## Proposed Stable Definitions

### Testing Adapter Contract Draft
- Publish `@webstir-io/testing-contract` package exporting the TypeScript definitions above (see `Framework/Contracts/testing-contract/src/index.ts`).
- Provide JSON Schema (Draft 2020-12) for automated validation (see `Framework/Contracts/testing-contract/schema/`):
  - `TestManifest.schema.json`
  - `RunnerEvent.schema.json`
- Require providers to:
  - Echo the exact `WEBSTIR_TEST ` prefix.
  - Guarantee event ordering (`start` → zero-or-more `result`/`log`/`error` → per-runtime `summary` → `summary` with `runtime: 'all'`).
  - Exit with non-zero status if any `RunnerSummaryEvent.summary.failed > 0` or an unhandled exception occurs.

### Module Provider Interface Draft
(Source: `Framework/Contracts/module-contract/src/index.ts`)

```ts
export type ModuleKind = 'frontend' | 'backend';

export interface ModuleProviderMetadata {
  readonly id: string;                 // e.g., '@webstir-io/frontend-default'
  readonly kind: ModuleKind;
  readonly version: string;            // Semver.
  readonly compatibility: ModuleCompatibility;
}

export interface ModuleCompatibility {
  readonly minCliVersion: string;
  readonly maxCliVersion?: string;
  readonly nodeRange: string;
  readonly notes?: string;
}

export interface ModuleProvider {
  readonly metadata: ModuleProviderMetadata;
  resolveWorkspace(options: ResolveWorkspaceOptions): Promise<ResolvedModuleWorkspace>;
  build(options: ModuleBuildOptions): Promise<ModuleBuildResult>;
  getScaffoldAssets?(): Promise<readonly ModuleAsset[]>;
}

export interface ResolveWorkspaceOptions {
  readonly workspaceRoot: string;
  readonly config: Record<string, unknown>;
}

export interface ResolvedModuleWorkspace {
  readonly sourceRoot: string;         // e.g., '<workspace>/src/frontend'
  readonly buildRoot: string;          // e.g., '<workspace>/build/frontend'
  readonly testsRoot: string;          // optional override of default `tests` path.
}

export interface ModuleBuildOptions {
  readonly workspaceRoot: string;
  readonly env: Record<string, string | undefined>;
  readonly incremental?: boolean;
}

export interface ModuleBuildResult {
  readonly artifacts: readonly ModuleArtifact[];
  readonly manifest: ModuleBuildManifest;
}

export interface ModuleArtifact {
  readonly path: string;               // Absolute file path.
  readonly type: 'asset' | 'bundle' | 'metadata';
}

export interface ModuleBuildManifest {
  readonly entryPoints: readonly string[];
  readonly staticAssets: readonly string[];
  readonly diagnostics: readonly ModuleDiagnostic[];
}

export interface ModuleDiagnostic {
  readonly severity: 'info' | 'warn' | 'error';
  readonly message: string;
  readonly file?: string;
}
```

**Key Behaviors**
- CLI discovers providers via configuration (e.g., `webstir.config.{ts,json}`) or package.json metadata.
- Default providers wrap existing modules and translate current behavior into the interface.
- Module build outputs feed directly into the test manifest (`buildRoot` + `testsRoot`) and publishing workflows.

## ADR Outline (Draft)
1. **Title**: Webstir Adapter Contracts for Testing and Module Providers.
2. **Status**: Proposed (Phase 0).
3. **Context**:
   - Monolithic runtime packaging causes release coupling.
   - CLI consumers need predictable contracts to author alternate runtimes/modules.
   - Existing implicit behavior (event stream, module layout) must be codified.
4. **Decision**:
   - Publish versioned contract packages (`@webstir-io/testing-contract`, `@webstir-io/module-contract`).
   - Enforce semver for breaking changes and require compatibility checkers in CI.
   - Provide default implementations for backwards compatibility.
5. **Consequences**:
   - Enables third-party providers.
   - Requires additional CI to ensure contract compliance.
   - Locks in external surface; future changes flow through ADR/governance.
6. **Implementation Plan**:
   - Generate schema artifacts from TypeScript definitions.
   - Embed schema validation in `webstir test` and module loading paths.
   - Document version negotiation and fallback strategies.

## Backlog & Open Questions
- **Runtime Enhancements**
  - Expand assertion library (deep equality, snapshots).
  - Introduce test timeouts and parallel execution where safe.
  - Improve watch mode diagnostics (initial build status, retry logging).
- **Module Provider Gaps**
  - Define how scaffolding selects module assets vs. defaults.
  - Clarify how multiple providers of the same kind are resolved or prioritized.
  - Determine configuration surface (`webstir.config.ts` vs. package.json fields).
  - Specify artifact naming conventions for publish pipelines.
- **Versioning / Compatibility**
  - Establish minimum CLI version that understands provider interfaces.
  - Decide on feature flags for gradual rollout (legacy vs. contract-based mode).
  - Plan migration path for existing projects pinned to tarball-based packages.
- **Documentation**
  - Produce how-to guides for authoring providers and verifying contract compliance.
  - Provide troubleshooting matrix for common schema violations or event ordering issues.

## Next Actions
- Review this packet with architecture and tooling owners; capture sign-off.
- Integrate JSON schema validation into CI and developer tooling using the artifacts under `Framework/Contracts/**/schema` (root script: `npm run validate:contracts`).
- Promote ADR 0001 (`Docs/product/adr/0001-testing-and-module-contracts.md`) to Accepted once the validation plan is in place.
- Stand up contract verification tests inside the repository (`dotnet` bridge + TypeScript provider).
- Introduce provider selection knobs (for example, `WEBSTIR_FRONTEND_PROVIDER`, `WEBSTIR_BACKEND_PROVIDER`) ahead of the pilot to simplify swaps.
