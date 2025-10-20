# Select Module Providers

Webstir can swap module providers without code changes. Use the environment variables below to override the defaults.

## Frontend

```bash
WEBSTIR_FRONTEND_PROVIDER=@webstir-io/vite-frontend webstir build
```

- `@webstir-io/webstir-frontend` (default)
- `@webstir-io/vite-frontend` (pilot – Vite build pipeline)

Or add a `webstir.providers.json` file to the workspace root:

```json
{
  "frontend": "@webstir-io/vite-frontend"
}
```

## Backend

```bash
WEBSTIR_BACKEND_PROVIDER=@webstir-io/webstir-backend webstir publish
```

Backend swaps will pick up any provider published under the module contract.

## Testing

```bash
WEBSTIR_TESTING_PROVIDER=@webstir-io/webstir-testing webstir test
```

- `@webstir-io/webstir-testing` — default VM-based provider bundled with the CLI.
- `@webstir-io/vitest-testing` — Vitest integration; install alongside your workspace with `npm install --save-dev @webstir-io/vitest-testing` then run `WEBSTIR_TESTING_PROVIDER=@webstir-io/vitest-testing webstir test`.

Or add a `webstir.providers.json` entry:

```json
{
  "testing": "@webstir-io/webstir-testing"
}
```

Quickstart for unpublished builds:

```bash
WEBSTIR_TESTING_PROVIDER=@webstir-io/vitest-testing \
WEBSTIR_TESTING_PROVIDER_SPEC=../vitest-testing \
webstir test
```

The host installs providers from `WEBSTIR_TESTING_PROVIDER_SPEC` when set; leave it empty when consuming from the registry. Point the value at your local clone of the standalone repository when testing unpublished builds.

## Notes

- The provider must implement `@webstir-io/module-contract` and be installed in the workspace.
- The module-host bridge proxies all output via the `.NET` host; log entries include provider id, entry points, and diagnostics.
- These knobs are temporary during the pilot; future phases will add config-driven selection (`webstir.config.ts`).
