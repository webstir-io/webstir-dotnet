# Requirements: CLI Environment

- Scope: app settings, environment variables, configuration sources, and safe exposure rules for the CLI host.

## Customer Requirements (CR)
- Zero-config by default; configure only when needed.
- Consistent, documented precedence across config sources.
- Avoid leaking secrets to the client.

## Product Requirements (PR)
- Typed settings object bound via DI (`AppSettings`).
- Read from: `appsettings.json` (optional), environment variables, and defaults.
- Defined precedence: env vars override file; code defaults last.
- Safe exposure: only explicitly whitelisted values can reach client bundles (future).

## Software Requirements (SR)
- Bind `AppSettings` from configuration in CLI host startup.
- Provide extension points for features to read config without tight coupling.
- Do not write config files; runtime-only reads.
- Add guardrails to prevent exposing server-only values to client code.

## Acceptance Criteria
- Changing an env var affects behavior without code changes.
- Missing `appsettings.json` does not break the app.
- No client artifact contains server-only secrets.
 
See also: [CLI Host hub](./cli-host.md)
