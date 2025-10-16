# @webstir-io/webstir-frontend

Webstir’s frontend build and publish toolkit. It packages the HTML/CSS/JS pipeline, scaffolding helpers, and module provider used by the Webstir CLI and installers once the packages live in a registry.

---

## Quick Start

1. **Authenticate to GitHub Packages**
   ```ini
   # .npmrc
   @webstir-io:registry=https://npm.pkg.github.com
   //npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
   ```
   Use a token with at least `read:packages`. Repository workflows expect `GH_PACKAGES_TOKEN` to be scoped for publish access.
2. **Install the package**
   ```bash
   npm install @webstir-io/webstir-frontend
   ```
3. **Run a build**
   ```bash
   npx webstir-frontend build --workspace /absolute/path/to/workspace
   ```

---

## Workspace Expectations

The toolkit assumes the standard Webstir workspace layout:

```
workspace/
  src/
    frontend/
      app/
      pages/
      images/
      fonts/
      media/
      frontend.config.json   # optional feature flag overrides
      webstir.config.mjs     # optional hook definitions
  build/frontend/…           # generated build artifacts
  dist/frontend/…            # publish-ready assets
  .webstir/manifest.json     # pipeline manifest emitted on each run
```

It requires Node.js **20.18.x** or newer (see `engines` in `package.json`).

---

## CLI Usage

The published binary is `webstir-frontend`. All commands require `--workspace` pointing to the absolute workspace root.

| Command | Description | Typical usage |
|---------|-------------|----------------|
| `build` | Runs the development build pipeline (incremental safe). | `npx webstir-frontend build --workspace $WORKSPACE` |
| `publish` | Produces optimized production assets under `dist/frontend`. | `npx webstir-frontend publish --workspace $WORKSPACE` |
| `rebuild` | Incremental rebuild after a file change. Accepts `--changed-file`. | `npx webstir-frontend rebuild --workspace $WORKSPACE --changed-file path/to/file` |
| `add-page <name>` | Scaffolds a new page (HTML/CSS/TS triple) inside `src/frontend/pages`. | `npx webstir-frontend add-page marketing/home --workspace $WORKSPACE` |
| `watch-daemon` | Persistent watcher that coordinates the pipeline and hot-module reload. Supports `--no-auto-start`, `--verbose`, and `--hmr-verbose`. | `npx webstir-frontend watch-daemon --workspace $WORKSPACE --verbose` |

### Feature Flags

An optional `frontend.config.json` controls build flags. Example:

```jsonc
{
  "features": {
    "htmlSecurity": true,
    "imageOptimization": true,
    "precompression": false
  }
}
```

### Lifecycle Hooks

Custom hooks live in the workspace root as `webstir.config.mjs` (or `.js`/`.cjs`). Export `hooks` with `pipeline.beforeAll/afterAll` and `builders.{name}.before/after` handlers:

```js
export const hooks = {
  pipeline: {
    beforeAll({ mode }) {
      console.info(`[webstir] starting ${mode} pipeline`);
    }
  },
  builders: {
    assets: {
      after({ config }) {
        // inspect generated assets
      }
    }
  }
};
```

---

## Programmatic API

The package exports a `ModuleProvider` that aligns with `@webstir-io/module-contract`. The provider drives installer workflows and can be embedded directly:

```ts
import { frontendProvider } from '@webstir-io/webstir-frontend';

const result = await frontendProvider.build({
  workspaceRoot: '/absolute/path/to/workspace',
  env: {
    WEBSTIR_MODULE_MODE: 'publish' // optional: defaults to "build"
  }
});

console.log(result.manifest.entryPoints);
```

- `frontendProvider.metadata` exposes version, CLI compatibility, and supported Node range.
- `frontendProvider.resolveWorkspace` returns canonical source/build/test directories for installers.
- `frontendProvider.build` runs the pipeline and returns the asset manifest used by the Webstir module synchronizer.

---

## Development (Maintainers)

```bash
npm install
npm run build         # TypeScript → dist/
npm run test          # Node test runner against dist/*
```

The repository ships GitHub Actions for `ci.yml` (lint/build/test) and `release.yml` (tag-driven publish). Local publishes target the GitHub Packages registry specified in `publishConfig`.

---

## Troubleshooting

- **“Authentication required for npm.pkg.github.com”** — ensure the token used in `.npmrc` has `read:packages`.
- **“No frontend test files found”** — the npm `test` script expects `tests/**/*.test.js` after build; generate fixtures or adjust the suite.
- **Missing entry points in manifest** — confirm `build/frontend` contains at least one `.js`/`.mjs` bundle. The pipeline falls back to `build/app/index.js` and emits a warning if empty.

---

## License

MIT © Webstir
