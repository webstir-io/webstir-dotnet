# Select Module Providers

Webstir can swap module providers without code changes. Use the environment variables below to override the defaults.

## Frontend

```bash
WEBSTIR_FRONTEND_PROVIDER=@webstir-io/webstir-frontend-vite webstir build
```

- `@webstir-io/webstir-frontend` (default)
- `@webstir-io/webstir-frontend-vite` (pilot – Vite build pipeline)

Or add a `webstir.providers.json` file to the workspace root:

```json
{
  "frontend": "@webstir-io/webstir-frontend-vite"
}
```

## Backend

```bash
WEBSTIR_BACKEND_PROVIDER=@webstir-io/webstir-backend webstir publish
```

Backend swaps will pick up any provider published under the module contract.

## Notes

- The provider must implement `@webstir-io/module-contract` and be installed in the workspace.
- The module-host bridge proxies all output via the `.NET` host; log entries include provider id, entry points, and diagnostics.
- These knobs are temporary during the pilot; future phases will add config-driven selection (`webstir.config.ts`).
