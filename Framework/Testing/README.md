# @webstir-io/webstir-testing

Unified test runner, runtime helpers, and CLI for Webstir TypeScript workspaces. Provides the binaries used by the Webstir CLI and the `test` API consumed inside generated specs.

## Quick Start

1. **Install**
   ```bash
   npm install --save-dev @webstir-io/webstir-testing
   ```
2. **Run tests**
   ```bash
   npx webstir-testing --workspace /absolute/path/to/workspace
   ```

Requires Node.js **20.18.x** or newer and assumes TypeScript has already produced compiled output in `build/**/tests/`.

## Workspace Layout

```
workspace/
  src/
    frontend/tests/*.test.ts
    backend/**/tests/*.test.ts
  build/
    frontend/tests/*.test.js
    backend/**/tests/*.test.js
```

Compile with `tsc` (or the workspace build) before invoking the runner; the CLI executes JavaScript from `build/`.

## CLI Commands

Binary aliases: `webstir-testing`, `webstir-testing-runner`, `webstir-testing-add`.

| Command | Description | Notable options |
|---------|-------------|-----------------|
| `webstir-testing` / `webstir-testing test` | Discovers and runs the suite once. | `--workspace <absolute path>` (defaults to `cwd`). |
| `webstir-testing watch` | Watches `src/` and reruns on change. | `--workspace`, `--debounce <ms>` (default 150). |
| `webstir-testing-add <name>` | Scaffolds a sample test file. | `--workspace` to control destination. |

Tips:
- Set `WEBSTIR_TEST_RUNTIME=<frontend|backend|all>` to limit discovery to a single runtime. This is the same flag the Webstir CLI passes when you invoke `webstir test --runtime backend`.

### Event Stream

Runner events emit to `stdout` prefixed with `WEBSTIR_TEST ` followed by JSON. Event types include `start`, `result`, `summary`, `watch-iteration`, `log`, and `error`. Downstream tooling can parse these payloads using `@webstir-io/testing-contract`.

## Runtime & APIs

```ts
import { test, assert } from '@webstir-io/webstir-testing';

test('adds numbers', () => {
  assert.equal(42, add(40, 2));
});
```

- `test(name, fn)` registers sync or async callbacks.
- `assert` exposes `isTrue`, `equal`, and `fail` (throws `AssertionError`).
- `discoverTestManifest(workspace)` builds the manifest consumed by the CLI.
- `createDefaultProviderRegistry()` returns a `ProviderRegistry` with default runtime handlers (`frontend`, `backend`).
- `run(files)` executes compiled modules and returns a `RunnerSummary`.

All exported types align with `@webstir-io/testing-contract`.

## Maintainer Workflow

```bash
npm install
npm run build          # TypeScript → dist/
```

- Add integration fixtures under `tests/` before enabling automated suites.
- Ensure CI runs `npm ci`, `npm run build`, and any smoke tests prior to publishing.
- Publishing targets npm per `publishConfig`.

## Troubleshooting

- **“No tests found under src/**/tests/.”** — ensure compiled JavaScript exists in `build/**/tests/`.
- **ESM/CommonJS errors** — the runtime attempts CommonJS first and falls back to dynamic `import()`; misconfigured TypeScript output may still surface syntax errors.
- **Watch mode exits non-zero** — inspect emitted `WEBSTIR_TEST` events for failures.

## License

MIT © Webstir
