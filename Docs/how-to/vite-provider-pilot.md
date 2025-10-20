# Pilot: Vite Frontend Provider

This guide shows how to exercise the Vite-based frontend module provider during the pilot.

## Prerequisites
- Workspace created with `webstir init` (fullstack or frontend).
- Dependencies restored (`npm install`).
- Provider package available: `@webstir-io/vite-frontend` (bundled with the repo during the pilot).

## Quick Start
1. Add `webstir.providers.json` to the workspace root:

    ```json
    {
      "frontend": "@webstir-io/vite-frontend"
    }
    ```

2. Run build/publish:

    ```bash
    webstir build
    webstir publish
    ```

Use `WEBSTIR_FRONTEND_PROVIDER` for ad-hoc overrides. Logs include provider id, entry points, and diagnostics streamed from the module host bridge.

## Watch Mode
```bash
WEBSTIR_FRONTEND_PROVIDER=@webstir-io/vite-frontend webstir watch
```

Hot-update diagnostics flow through the same provider manifest; tail the CLI output to validate HMR behaviour.

> Tip: For unpublished builds from the standalone repository, set `WEBSTIR_FRONTEND_PROVIDER_SPEC=<path-to-local-vite-frontend>` (for example `../vite-frontend`) so the host installs your local checkout.

## Notes
- Provider selection also affects `webstir test` when it triggers frontend builds.
- Future phases will expose provider selection via `webstir.config.ts`.
- Backend swaps use `WEBSTIR_BACKEND_PROVIDER` (see `Docs/how-to/provider-selection.md`).
- Regression coverage: `Tester.Workflows.Build.BuildWorkflowTests.BuildWithViteProviderProducesArtifacts` exercises the provider end-to-end.
