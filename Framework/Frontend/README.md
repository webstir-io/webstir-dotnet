# @webstir-io/webstir-frontend

Frontend build and publish toolkit for Webstir workspaces. The package bundles the HTML/CSS/JS pipeline, scaffolding helpers, and module provider used by the Webstir CLI and installers.

## Quick Start

1. **Authenticate to GitHub Packages**
   ```ini
   # .npmrc
   @webstir-io:registry=https://npm.pkg.github.com
   //npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
   ```
   Use a token with `read:packages` for consumers and `write:packages` for publishers.
2. **Install the package**
   ```bash
   npm install @webstir-io/webstir-frontend
   ```
3. **Run a build**
   ```bash
   npx webstir-frontend build --workspace /absolute/path/to/workspace
   ```

Requires Node.js **20.18.x** or newer.

## Workspace Layout

The provider assumes the standard Webstir workspace shape:

```
workspace/
  src/frontend/
    app/
    pages/
    images/
    fonts/
    media/
    frontend.config.json   # optional feature flag overrides
    webstir.config.mjs     # optional hook definitions
  build/frontend/...       # generated build artifacts
  dist/frontend/...        # publish-ready assets
  .webstir/manifest.json   # pipeline manifest emitted on each run
```

## CLI Commands

Binary name: `webstir-frontend`. All commands require `--workspace` pointing to the absolute workspace root.

| Command | Description | Useful options |
|---------|-------------|----------------|
| `build` | Runs the development pipeline (incremental safe). | `--changed-file <path>` to scope rebuilds. |
| `publish` | Produces optimized assets under `dist/frontend`. | — |
| `rebuild` | Incremental rebuild triggered by a file change. | `--changed-file <path>` to pass the changed file. |
| `add-page <name>` | Scaffolds a page (HTML/CSS/TS) inside `src/frontend/pages`. | — |
| `watch-daemon` | Persistent watcher + HMR coordinator. | `--no-auto-start`, `--verbose`, `--hmr-verbose`. |

### Feature Flags

`frontend.config.json` enables or disables pipeline features:

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

Hooks live in `webstir.config.mjs` (or `.js`/`.cjs`) at the workspace root:

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
        // custom post-processing
      }
    }
  }
};
```

## API Usage

The package exports a `ModuleProvider` compatible with `@webstir-io/module-contract`:

```ts
import { frontendProvider } from '@webstir-io/webstir-frontend';

const result = await frontendProvider.build({
  workspaceRoot: '/absolute/path/to/workspace',
  env: { WEBSTIR_MODULE_MODE: 'publish' }
});

console.log(result.manifest.entryPoints);
```

- `frontendProvider.metadata` surfaces id/version compatibility.
- `frontendProvider.resolveWorkspace` returns canonical source/build/test paths.
- `frontendProvider.build` executes the pipeline and returns artifacts + manifest.

## Maintainer Workflow

```bash
npm install
npm run build          # TypeScript → dist/
npm run test           # Node --test against compiled output
```

GitHub Actions should run `npm ci`, `npm run build`, and `npm run test` before publishing. The package publishes to GitHub Packages per `publishConfig`.

## Troubleshooting

- **“Authentication required for npm.pkg.github.com”** — ensure the configured token has `read:packages`.
- **“No frontend test files found”** — the `test` script expects files under `tests/**/*.test.js` after build.
- **Missing entry points in manifest** — confirm `build/frontend` contains at least one `.js`/`.mjs` bundle; the provider falls back to `build/app/index.js` and emits a warning if empty.

## License

MIT © Webstir
