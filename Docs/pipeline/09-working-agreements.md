# Working Agreements (Framework Pipeline)

This document captures time‑boxed, feature‑specific agreements for the current framework pipeline work so our general patterns doc stays evergreen.

## Scope & Location
- All work lives under `Engine/Pipelines/*` (use `Engine/Pipelines/Core` for primitives needed by multiple pipelines).

## JavaScript/TypeScript
- TypeScript is required for the JS pipeline; fail fast with an actionable message if `tsc` is unavailable.
- Tokenizer scope is dependency extraction only:
  - Import/Export declarations (static) and re‑exports.
  - Detect `import('literal')` as a dynamic import; ignore non‑literal.
- No JSX or CommonJS support in v1.
- Keep existing transformers/minification; tokenizer does not perform code transforms.

## CSS
- Tokenizer reads `@import` (including `url()` form) and optional media query tail.
- URL rewriting, CSS Modules, minification remain as they are.

## Parser Conventions
- Names: `ImportDeclaration`, `ExportDeclaration` (JS); `CssImportRule` (CSS at‑rule).
- Methods: `ParseImports()`, `ParseExports()`.
- Skip non‑semantic tokens via `SkipTrivia()` (whitespace/newlines/comments).
- On ambiguity, add a diagnostic and optionally fall back to the legacy path.

## Constants (No Magic Strings)
- Do not introduce magic strings for filenames, extensions, or folders in pipelines.
- Use `Engine/Constants.cs` (`Files`, `FileExtensions`, `Folders`) for shared literals.

## Diagnostics
- Use `Engine/Pipelines/Core/Diagnostics.cs`.
- Include file, line, column where possible; keep messages actionable.

## Cache Busting (micro‑phase)
- Fingerprint outputs as `index.<timestamp>.js/css`.
- Per‑page manifest at `dist/pages/<page>/manifest.json` containing `{ js, css, map? }`.
- HTML publish rewrites asset references from the manifest.
- Dev: no‑cache headers; Prod: long‑cache for fingerprinted assets.

## Validation
- Ensure import/export and `@import` are identified correctly across typical cases (comments, escapes, whitespace).

## Out of Scope (v1)
- JSX handling; React/Next integrations; SSR.
- CommonJS interop.
