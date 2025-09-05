# Config & Env

## Current (Webstir)
- `AppSettings` for ports/URLs; Node server reads `PORT`, `WEB_SERVER_URL`, `API_SERVER_URL`.
- No framework config file; env exposure strategy unspecified.

## Baseline (Others)
- Next/Remix/SvelteKit: config files, mode (`development`/`production`/`test`), env loading (.env), safe client env exposure.

## Gaps to Parity
- (P2) Framework config for routing/SSR/SSG/options; mode handling.
- (P3) `.env` loading, schema validation, and client env prefixing.

## Parity Plan
- Extend `webstir.config.*` (see bundling Config & Extensibility) with framework block: routing, SSR/SSG, actions, auth.
- Add env loader with `WEBSTIR_*` client‑exposed prefix and validation.

