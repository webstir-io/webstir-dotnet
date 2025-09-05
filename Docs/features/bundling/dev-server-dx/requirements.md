# Requirements: Dev Server & DX

- Scope: HMR, error overlay, HTTPS/proxy, CORS options.

## Customer Requirements (CR)
- Fast feedback while editing with minimal full page reloads.
- Clear error messages in the browser with code frames and links.
- Simple HTTPS enablement and flexible proxy rules.
- Sensible defaults with opt‑in advanced behavior.

## Product Requirements (PR)
- ESM/CSS HMR in dev mode.
- In‑browser error overlay with code frames and clickable links to files.
- HTTPS support (self‑signed certs) and configurable proxy rules.
- Configurable CORS for local development when needed.

## Software Requirements (SR)
- Track the ESM graph; send HMR updates over WebSocket for changed modules and CSS.
- Overlay client script that renders build/runtime errors with frames; toggle via settings.
- HTTPS: accept certificate/key paths or generate ephemeral self‑signed certs for dev.
- Proxy: route matching paths to targets with rewrite, headers, and WebSocket support.
- CORS: opt‑in allowlist and methods/headers configuration.

## Acceptance Criteria
- Editing a JS module triggers HMR without full reload; editing CSS swaps styles in place.
- A thrown error shows an overlay with a stack and code frame; link opens the file in the editor if supported.
- Enabling HTTPS serves the dev server over TLS; proxy routes traffic as configured.
