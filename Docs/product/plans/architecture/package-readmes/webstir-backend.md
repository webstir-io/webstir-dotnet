# @webstir-io/webstir-backend

Backend build orchestration for Webstir workspaces. The package exposes a `ModuleProvider` that compiles TypeScript sources, discovers build artifacts, and reports diagnostics back to the Webstir CLI/installer flow.

---

## Quick Start

1. **Authenticate to GitHub Packages**
   ```ini
   # .npmrc
   @webstir-io:registry=https://npm.pkg.github.com
   //npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
   ```
   The `GH_PACKAGES_TOKEN` secret needs `read:packages` for consumers and `write:packages` for publish workflows.
2. **Install the provider**
   ```bash
   npm install @webstir-io/webstir-backend
   ```
3. **Invoke the module build**
   ```ts
   import { backendProvider } from '@webstir-io/webstir-backend';

   const result = await backendProvider.build({
     workspaceRoot: '/absolute/path/to/workspace',
     env: {},
     incremental: true
   });

   console.log(result.manifest.entryPoints);
   ```

---

## Workspace Layout

The provider assumes the canonical Webstir backend structure:

```
workspace/
  src/
    backend/
      tsconfig.json
      index.ts
      handlers/
      tests/
  build/backend/…      # compiled JS output (tsc outDir)
  dist/backend/…       # optional publish artifacts
```

Key requirements:

- `src/backend/tsconfig.json` must exist; the provider shells out to `tsc -p`.
- Output defaults to `build/backend` (configurable via `tsconfig.json`).
- The manifest expects at least one `index.js` bundle under the build directory.
- Node.js **20.18.x** or newer (see `engines`).

---

## Module Provider API

`backendProvider` implements `ModuleProvider` from `@webstir-io/module-contract`:

- `metadata` exposes package id/version, kind (`backend`), CLI compatibility, and supported Node range.
- `resolveWorkspace({ workspaceRoot })` returns `sourceRoot`, `buildRoot`, and `testsRoot` locations, used by installers to wire file operations.
- `build(options)` orchestrates TypeScript compilation, gathers `.js` outputs via `glob`, and constructs a manifest:
  - Honors `options.incremental` to pass `--incremental` to `tsc`.
  - Infers pipeline mode (`build`, `publish`, or `test`) from `options.env.WEBSTIR_MODULE_MODE`, defaulting `NODE_ENV`.
  - Aggregates `ModuleDiagnostic`s when compilation fails or entry points are missing.

Example integration in a module synchronizer:

```ts
const buildResult = await backendProvider.build({
  workspaceRoot,
  env: { WEBSTIR_MODULE_MODE: 'publish' }
});

for (const artifact of buildResult.artifacts) {
  await upload(artifact.path);
}
```

---

## NPM Scripts

| Script | Description |
|--------|-------------|
| `npm run build` | Compiles `src/` TypeScript to `dist/` using the repo `tsconfig.json`. |
| `npm run clean` | Removes the generated `dist/` directory. |

The published package ships prebuilt `.d.ts` + `.js` files in `dist/`.

---

## Maintainer Workflow

```bash
npm install
npm run build          # emit dist/provider.js + types
```

Add tests under `tests/**/*.test.ts` (compiled alongside sources) and extend GitHub Actions (`ci.yml`, `release.yml`) to run lint/build/test before publishing. Publishing is configured for GitHub Packages via `publishConfig.registry`.

---

## Troubleshooting

- **`TypeScript config not found` warnings** — ensure `src/backend/tsconfig.json` exists; the provider skips compilation otherwise.
- **`Backend TypeScript compilation failed` diagnostics** — inspect attached stderr/stdout entries in the manifest, and rerun `tsc -p src/backend/tsconfig.json` locally.
- **`No backend entry point found` warning** — make sure `build/backend/index.js` (or another `index.js` somewhere in the tree) exists after compilation.

---

## License

MIT © Webstir
