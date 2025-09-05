# Requirements: Transforms & Assets

- Scope: TS/JSX transforms, defines/macros, JSON/text/asset loaders, sourcemaps, native CSS.

## Customer Requirements (CR)
- Write TS/JSX and get fast, correct output in dev and prod.
- Configure environment defines to strip dead branches in production.
- Import JSON/text and assets with simple, consistent behavior.
- Use native CSS features by default (no PostCSS/Sass required).

## Product Requirements (PR)
- TypeScript/JSX transforms with inline/external sourcemaps as configured.
- Defines/macros with dead‑branch elimination for production builds.
- JSON/text/asset loaders (url/file/inline with size limits) with consistent URL rewriting.
- Native CSS support (nesting, `@layer`) without PostCSS by default.

## Software Requirements (SR)
- Implement TS/JSX transformation with target selection and sourcemap generation.
- Preserve license comments per policy in production builds.
- Replace configured symbols (e.g., `process.env.*`) and fold constants before minification.
- Provide loaders:
  - JSON: default export object.
  - Text: default export string.
  - Assets: inline below size threshold, otherwise emit file and export URL.
- CSS: rely on native features; document unsupported features and opt‑in extensions if any.

## Acceptance Criteria
- A TSX sample compiles in dev and prod with correct sourcemaps.
- Setting defines removes dev‑only branches from production output.
- `import data from './file.json'` returns parsed JSON; small images inline, large images emit URLs.
- CSS files load without extra config; nesting works with supported targets.
