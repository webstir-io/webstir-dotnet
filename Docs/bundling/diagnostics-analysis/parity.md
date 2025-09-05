# Diagnostics & Analysis

## Current (Webstir)
- Diagnostics collection and logging for build/publish phases; warnings for HTML/CSS issues.
- No bundle size/stats output; no analyzer.
- No TypeScript type‑check gate in publish (tsc only in build); no ESLint integration.
- No license extraction or banner preservation.
- No gzip/brotli size reporting.

## Baseline (Others)
- Webpack/Rspack: stats JSON, bundle analyzers, type/lint plugins, license plugins, profile/tracing.
- Rollup/Vite: visualizer/analyzers; TS/ESLint integrations; warnings with code frames.
- esbuild: metafile analysis; plugins for lint/license; timing.
- Turbopack: profiling and tracing tools; framework overlays.

## Gaps to Parity
- (P5) Bundle stats/analyzer and duplicate detection.
- (P6) Type/lint optional gates; code‑frame diagnostics.
- (P6) License extraction and comment preservation.
- (P6) Integrity hashes and gzip/brotli size reporting.

## Parity Plan
- Emit build stats (module/chunk sizes) and provide an analyzer HTML report.
- Offer optional `tsc --noEmit` and ESLint steps with friendly output.
- Implement license banner extraction/preservation and third‑party report.
- Compute integrity hashes and output compressed sizes in publish logs.
