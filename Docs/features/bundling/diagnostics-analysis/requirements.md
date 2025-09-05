# Requirements: Diagnostics & Analysis

- Scope: analyzer/stats, TypeScript compile gate, license report, integrity and size reporting.

## Customer Requirements (CR)
- Clear visibility into what the build produced and why it’s large.
- Fast feedback on type errors; fail early when types break.
- Compliance signals: license info and integrity hashes.

## Product Requirements (PR)
- Analyzer/stats report (sizes, duplicates) with an HTML report.
- TypeScript compile gate that runs in dev/build/CI and fails on errors.
- License extraction/report and banner preservation.
- Integrity hashes + gzip/brotli size reporting.

## Software Requirements (SR)
- Collect module/chunk size data; detect duplicates and emit a human‑readable HTML report.
- Invoke TypeScript in a compile‑only mode; surface diagnostics in CLI and overlay.
- Scan outputs for licenses; emit a summary and preserve required banners.
- Compute integrity hashes and compressed sizes; add to manifest/report.

## Acceptance Criteria
- Running a build emits a stats report with chunk/module sizes and duplicate highlights.
- Type errors cause the dev server overlay and fail CI builds.
- A license summary is generated and banners appear in production outputs.
- The manifest or report includes integrity and gzip/brotli sizes.
