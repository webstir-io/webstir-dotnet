# JavaScript/TypeScript Pipeline (TS Required)

## Goal
Modern ESM bundling with TypeScript required. Tokenizer-based import/export parsing for graphing; `tsc` for TS transpilation.

## Assumptions
- Node.js + TypeScript are required runtime dependencies.
- ES modules only for v1 (no CommonJS transformation). Revisit CJS after parity.
- No framework coupling (no React/Next.js). No JSX support in v1.

## Flow
1. Compile TypeScript via `tsc` into a temporary folder (respecting source maps as option).
2. For each JS file (compiled or original):
   - Tokenize + parse imports/exports for dependency graph.
   - Build module graph (absolute resolved paths), detect cycles, compute order.
3. Transform and bundle:
   - Keep current bundler/runtime structure.
   - Replace import lines using parsed data; export lines mapped to `module.exports` object shape.
   - Optionally perform scope hoisting and basic tree-shaking (as today).
4. Generate source maps (coarse: per-module offsets, refine later).

## File Types
- Accepted entries: `.ts`, `.js`.
- Not accepted (v1): `.tsx`, `.jsx` (no JSX transform or React runtime expectations).

## TypeScript Config (generated)
- Exclude JSX settings entirely; do not set `jsx` option.
- Target modern ES (e.g., `es2020`), `module` set to `es2020` or similar.

## Diagnostics
- Report `tsc` output as structured diagnostics with file/line/column.
- Propagate tokenizer/parser diagnostics when dependency extraction fails.

## Validation Checklist
- `tsc` invoked with a generated temporary `tsconfig.bundle.json` aligned with framework options.
- Dependency graph includes only internal (non-external) modules.
- Build succeeds for mixed JS/TS projects.

## Out-of-Scope (v1)
- CommonJS import/export interop and conversion.
- Advanced code-splitting; focus on stable single-bundle path first.
