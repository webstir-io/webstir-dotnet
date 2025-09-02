# JavaScript/TypeScript Pipeline (TS Required)

## Goal
Modern ESM bundling with TypeScript required. Tokenizer-based import/export parsing for graphing; `tsc` for TS transpilation.

## Assumptions
- Node.js + TypeScript are required runtime dependencies.
- ES modules only for v1 (no CommonJS transformation). Revisit CJS after parity.
- No framework coupling (no React/Next.js). No JSX support in v1.

## Flow
1. Compile TypeScript via `tsc` into the build folder using the repo’s `base.tsconfig.json` with project references (`src/shared`, `src/client`, `src/server`).
2. For each JS file (compiled or original):
   - Tokenize + parse imports/exports for dependency graph.
   - Build module graph (absolute resolved paths), detect cycles, compute order.
3. Transform and bundle:
   - Keep current bundler/runtime structure.
   - Replace import lines using parsed data; export lines mapped to `module.exports` object shape.
   - Optionally perform scope hoisting and basic tree-shaking (as today).
4. Generate source maps (coarse: per‑module offsets). Emit separate `.map` files in dist; consider inline maps for dev later.

## File Types
- Accepted entries: `.ts`, `.js`.
- Not accepted (v1): `.tsx`, `.jsx` (no JSX transform or React runtime expectations).

## TypeScript Config
- Use the embedded `base.tsconfig.json` with per-package `tsconfig.json` files under `src/client`, `src/shared`, and `src/server`.
- Exclude JSX settings entirely; do not set `jsx` option.
- Target modern ES (e.g., `es2020`/`ES2022`), `module` set to `esnext` for client, server may use `commonjs` as today.

## Diagnostics
- Report `tsc` output as structured diagnostics with file/line/column.
- Propagate tokenizer/parser diagnostics when dependency extraction fails.

## Validation Checklist
- `tsc` runs against `base.tsconfig.json` and project references; outputs to `build/client`.
- Dependency graph includes only internal (non-external) modules.
- Build succeeds for mixed JS/TS projects.
- Bundled JS has a matching `.map` file emitted in dist.

## Out-of-Scope (v1)
- CommonJS import/export interop and conversion.
- Advanced code-splitting; focus on stable single-bundle path first.
