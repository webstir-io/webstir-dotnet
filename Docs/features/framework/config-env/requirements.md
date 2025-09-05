# Requirements: Config & Env

- Scope: mode handling (dev/prod/test), `.env` loading, and safe client env exposure.

## Customer Requirements (CR)
- Clear modes with sensible defaults.
- Load environment variables from files and validate them.
- Expose only safe env vars to the client.

## Product Requirements (PR)
- Mode handling: `development`, `production`, `test` with consistent behavior.
- `.env` loading and schema validation with helpful errors.
- Client‑exposed env limited to a prefix (e.g., `WEBSTIR_*`).

## Software Requirements (SR)
- Read `.env` files by mode; merge with process env; validate against a schema.
- Provide `define` values to bundling for client exposure with prefix filtering.
- Document precedence: CLI/env > config > defaults.

## Acceptance Criteria
- `.env` is loaded and invalid values cause a clear error.
- Only `WEBSTIR_*` variables appear in client code; others do not.
