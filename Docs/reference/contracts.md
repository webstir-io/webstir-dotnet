# Contracts & Invariants

Public guarantees that Webstir enforces and that tests lock down.

## Sources
- Paths and structure — see Explanations: Workspace
- Build/publish behavior — see Explanations: Pipelines
- Dev server proxy/reload — see Explanations: Services and Servers

## Invariants
- Source roots:
  - `src/client/**`, `src/server/**`, `src/shared/**`, `types/**`
- Dev outputs:
  - `build/client/**`, `build/server/**`
- Prod outputs per page (publish):
  - `dist/client/pages/<page>/index.html`
  - `dist/client/pages/<page>/index.<timestamp>.{css|js}`
  - `dist/client/pages/<page>/manifest.json`
- App assets under `src/client/app/*` are copied as-is (dev and publish variants as applicable).
- Base HTML requires `<main>` in `src/client/app/app.html`.
- Dev server proxies `/api/*` to the Node server during `watch`.
- CLI exit codes are non-zero for invalid flags, missing inputs, build/test failures.

## Related Docs
- CLI reference — cli.md
- Workflows — workflows.md
- Workspace — ../explanations/workspace.md
- Pipelines — ../explanations/pipelines.md
- Services — ../explanations/services.md
- Servers — ../explanations/servers.md
