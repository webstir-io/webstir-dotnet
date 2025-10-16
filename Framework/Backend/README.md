# @webstir-io/webstir-backend

Backend build orchestration for Webstir workspaces. The package exposes a `ModuleProvider` that compiles backend TypeScript, collects build artifacts, and returns diagnostics for the Webstir CLI and installers.

## Quick Start

1. **Authenticate to GitHub Packages**
   ```ini
   # .npmrc
   @webstir-io:registry=https://npm.pkg.github.com
   //npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
   ```
   Consumers need `read:packages`; publishers also require `write:packages`.
2. **Install**
   ```bash
   npm install @webstir-io/webstir-backend
   ```
3. **Run a build**
   ```ts
   import { backendProvider } from '@webstir-io/webstir-backend';

   const { manifest } = await backendProvider.build({
     workspaceRoot: '/absolute/path/to/workspace',
     env: {},
     incremental: true
   });

   console.log(manifest.entryPoints);
   ```

Requires Node.js **20.18.x** or newer.

## Workspace Layout

```
workspace/
  src/backend/
    tsconfig.json
    index.ts
    handlers/
    tests/
  build/backend/...    # compiled JS output
```

`tsc -p src/backend/tsconfig.json` must succeed; the provider shells out to `tsc` and expects the compiled output under `build/backend`.

## Provider Contract

`backendProvider` implements `ModuleProvider` from `@webstir-io/module-contract`:

- `metadata` — package id, version, kind (`backend`), CLI compatibility, Node range.
- `resolveWorkspace({ workspaceRoot })` — returns canonical source/build/test roots.
- `build(options)` — invokes `tsc`, gathers `.js` artifacts, and emits a manifest describing entry points and diagnostics. Honors `options.incremental` and detects `options.env.WEBSTIR_MODULE_MODE` (`build`/`publish`/`test`) to set `NODE_ENV`.

Artifacts are returned as absolute paths so installers can copy or upload them. A missing `index.js` triggers a warning diagnostic.

## NPM Scripts

| Script | Description |
|--------|-------------|
| `npm run build` | Compiles TypeScript from `src/` into `dist/`. |
| `npm run clean` | Removes `dist/`. |

The published package ships prebuilt JavaScript and type definitions in `dist/`.

## Maintainer Workflow

```bash
npm install
npm run build          # emits dist/
```

- Add tests under `tests/**/*.test.ts` and wire them into `npm test` once the backend runtime is ready.
- Ensure CI runs `npm ci`, `npm run build`, and any smoke tests before publish.
- Publishing targets GitHub Packages via `publishConfig.registry`.

## Troubleshooting

- **`TypeScript config not found` warning** — ensure `src/backend/tsconfig.json` exists.
- **`Backend TypeScript compilation failed`** — inspect diagnostics (stderr/stdout captured in the manifest) and rerun `tsc -p`.
- **No backend entry point found** — confirm `build/backend/index.js` exists after compilation or adjust the build output.

## License

MIT © Webstir
