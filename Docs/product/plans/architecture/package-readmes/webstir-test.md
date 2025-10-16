# @webstir-io/webstir-test

Unified test runner, runtime helpers, and CLI for Webstir TypeScript workspaces. Ships the binaries used by the monorepo installers as well as the `test` API consumed inside generated specs.

---

## Quick Start

1. **Authenticate to GitHub Packages**
   ```ini
   # .npmrc
   @webstir-io:registry=https://npm.pkg.github.com
   //npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
   ```
   Provision `GH_PACKAGES_TOKEN` with `read:packages` (add `write:packages` for publishers).
2. **Install the toolkit**
   ```bash
   npm install --save-dev @webstir-io/webstir-test
   ```
3. **Run tests**
   ```bash
   npx webstir-test --workspace /absolute/path/to/workspace
   ```

Requires Node.js **20.18.x** or newer.

---

## Workspace Layout & Compilation

The CLI discovers tests under `src/**/tests/` and executes compiled JavaScript from `build/**/tests/…`. Typical structure:

```
workspace/
  src/
    frontend/
      tests/
        app.test.ts
    backend/
      handlers/
        tests/
          handler.test.ts
  build/
    frontend/tests/app.test.js
    backend/handlers/tests/handler.test.js
```

Best practice is to compile TypeScript before running `webstir-test`; the CLI does not invoke `tsc` automatically.

---

## CLI Commands

Binary names: `webstir-test` (primary), `webstir-test-runner` (alias), and `webstir-test-add`.

| Command | Description | Common options |
|---------|-------------|----------------|
| `webstir-test` / `webstir-test test` | Runs the full suite once. | `--workspace <absolute path>` (defaults to `process.cwd()`). |
| `webstir-test watch` | Watches `src/` and reruns on change. | `--workspace`, `--debounce <ms>` (default 150ms). |
| `webstir-test-add <name>` | Scaffolds a sample test under the workspace. Accepts slash-separated nested paths. | `--workspace <absolute path>`. |

### Watch Output

All runner events stream to `stdout` prefixed with `WEBSTIR_TEST ` followed by JSON. Tooling can subscribe to:

- `start` — test discovery complete
- `result` — per-test outcomes
- `summary` — per-runtime aggregate and final overall summary
- `watch-iteration` — watch loop lifecycle notifications
- `log` / `error` — diagnostics raised during execution

---

## Runtime & APIs

Common imports from `@webstir-io/webstir-test`:

```ts
import { test, assert } from '@webstir-io/webstir-test';

test('adds numbers', () => {
  assert.equal(42, add(40, 2));
});
```

- `test(name, fn)` registers synchronous or async callbacks.
- `assert` exposes `isTrue`, `equal`, and `fail`; throws `AssertionError`.
- `run(files)` (re-exported) executes compiled modules and returns a `RunnerSummary`.
- `discoverTestManifest(workspace)` builds the manifest consumed by the CLI.
- `createDefaultProviderRegistry()` yields a `ProviderRegistry` mapping runtimes (`frontend`, `backend`) to execution providers.

The package depends on `@webstir-io/testing-contract` for manifest and event typings; consumers may use those types directly.

---

## Maintainer Workflow

```bash
npm install
npm run build          # emits dist/*.js and .d.ts
```

- Add integration fixtures under `tests/` (compiled alongside sources) before enabling `npm run test`.
- GitHub Actions (`ci.yml`, `release.yml`) should run `npm ci`, `npm run build`, and any smoke suites prior to publishing.
- `publishConfig.registry` points to GitHub Packages; ensure `GH_PACKAGES_TOKEN` is available in repository secrets.

---

## Troubleshooting

- **“No tests found under src/**/tests/.”** — confirm compiled outputs exist in `build/**/tests/` or adjust the workspace structure.
- **Runtime errors referencing `Cannot use import statement`** — ensure `tsc` emits CommonJS (or that the runtime can evaluate ES modules); the runner automatically falls back to dynamic `import()` when CommonJS evaluation fails.
- **Watch mode exits with non-zero code** — failed iterations propagate exit codes; inspect `WEBSTIR_TEST` event logs for details.

---

## License

MIT © Webstir
