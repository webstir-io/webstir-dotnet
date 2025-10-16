# Pilot: Vite Frontend Provider

This guide shows how to exercise the Vite-based frontend module provider during the pilot.

## Prerequisites
- Workspace created with `webstir init` (fullstack or frontend).
- Dependencies restored (`npm install`).
- Provider package available: `@webstir-io/webstir-frontend-vite` (bundled with the repo during the pilot).

## Quick Start
1. Add `webstir.providers.json` to the workspace root:

    ```json
    {
      "frontend": "@webstir-io/webstir-frontend-vite"
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
WEBSTIR_FRONTEND_PROVIDER=@webstir-io/webstir-frontend-vite webstir watch
```

Hot-update diagnostics flow through the same provider manifest; tail the CLI output to validate HMR behaviour.

## Notes
- Provider selection also affects `webstir test` when it triggers frontend builds.
- Future phases will expose provider selection via `webstir.config.ts`.
- Backend swaps use `WEBSTIR_BACKEND_PROVIDER` (see `Docs/how-to/provider-selection.md`).
- Regression coverage: `Tester.Workflows.Build.BuildWorkflowTests.BuildWithViteProviderProducesArtifacts` exercises the provider end-to-end.
