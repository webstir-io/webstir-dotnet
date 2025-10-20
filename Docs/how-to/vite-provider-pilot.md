# Vite Frontend Provider

This guide shows how to use the Vite-powered frontend module provider with Webstir.

## Prerequisites
- Workspace created with `webstir init` (fullstack or frontend).
- Dependencies restored (`npm install`).
- Provider package available: `@webstir-io/vite-frontend` (install from the registry).

## Quick Start
1. Install the provider (once per workspace):

    ```bash
    npm install --save-dev @webstir-io/vite-frontend
    ```

2. Update `webstir.providers.json` in the workspace root:

    ```json
    {
      "frontend": "@webstir-io/vite-frontend"
    }
    ```

3. Synchronize dependencies:

    ```bash
    webstir install
    ```

4. Run build/publish:

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
- Provider selection also affects `webstir install`; keep `webstir.providers.json` in sync with the dependencies committed to the workspace.
- Backend swaps use `WEBSTIR_BACKEND_PROVIDER` (see `Docs/how-to/provider-selection.md`).
- Regression coverage: `Tester.Workflows.Build.BuildWorkflowTests.BuildWithViteProviderProducesArtifacts` exercises the provider end-to-end.
