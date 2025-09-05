# Requirements: Platform & Advanced

- Scope: workers/service worker, SSR/SSG pipeline, WASM, polyfills, remote modules.

## Customer Requirements (CR)
- Use web workers and service workers with minimal setup.
- Render pages on the server or at build time with hydration.
- Import and ship WASM where needed.
- Control polyfills per target without heavy defaults.
- Optionally consume remote modules via import maps.

## Product Requirements (PR)
- Workers/service worker bundling + registration helpers.
- SSR/SSG pipeline (server bundle + hydration maps; streaming where possible).
- WASM pipeline (copy/inline + async loader glue).
- Polyfills per target (document‑first; opt‑in injection).
- Remote modules via ESM import maps (optional).

## Software Requirements (SR)
- Treat worker entries as isolated bundles; emit registration helpers for service workers.
- SSR: emit a Node/edge server bundle; generate hydration maps/assets for the client.
- SSG: prerender routes to HTML with asset links and metadata.
- WASM: copy or inline; generate async loader that returns instantiated modules.
- Polyfills: determine per‑target needs; inject only on opt‑in.
- Import maps: support loading and merging project maps for remote modules.

## Acceptance Criteria
- A simple worker (`new Worker(...)`) builds and runs; a service worker registers and controls pages.
- SSR sample renders on the server and hydrates on the client; SSG outputs static HTML.
- Importing a WASM module yields a working instance via an async loader.
- Polyfills only appear when configured; remote import maps resolve as expected.
