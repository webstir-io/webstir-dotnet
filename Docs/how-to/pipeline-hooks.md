# Extend Pipelines with Hooks

Webstir exposes optional hook points so you can layer custom logic onto the
frontend build/publish pipelines without forking the framework packages. Hooks
run after the CLI verifies package versions, so they always execute against
the pinned dependencies from `webstir install`.

## Create `webstir.config.js`
Place a `webstir.config.js` file at the workspace root (next to `package.json`).
Export an object with a `hooks` section—pipeline hooks run before/after the
entire build, and builder hooks run around specific stages.

```js
// webstir.config.js
import fs from 'node:fs/promises';
import path from 'node:path';

async function record(event, context) {
  const logPath = path.join(context.workspaceRoot, '.tools', 'pipeline.log');
  const payload = JSON.stringify({ event, mode: context.mode, builder: context.builderName ?? null });
  await fs.appendFile(logPath, `${payload}\n`, 'utf8');
}

export default {
  hooks: {
    pipeline: {
      beforeAll: (context) => record('pipeline:start', context),
      afterAll: (context) => record('pipeline:complete', context)
    },
    builders: {
      // builder names: javascript, css, html, static-assets
      javascript: {
        before: (context) => record('javascript:before', context),
        after: (context) => record('javascript:after', context)
      },
      html: (context) => record('html:before', context) // shorthand for before hook
    }
  }
};
```

Hooks can be a single function or an array. Returning a promise is optional—the
pipeline awaits each handler in order before moving on.

## Hook Context
Every handler receives a `context` object:

| Field           | Description                                              |
|-----------------|----------------------------------------------------------|
| `config`        | The resolved `FrontendConfig` with all paths/features.    |
| `mode`          | `'build'` or `'publish'`.                                |
| `workspaceRoot` | Absolute workspace path.                                 |
| `builderName`   | Stage name (`javascript`, `css`, `html`, `static-assets`) or `undefined` for pipeline hooks. |
| `changedFile`   | Path supplied by incremental rebuilds, when available.   |

Use these values to locate files, run additional tooling, or emit diagnostics.
Throwing from a hook fails the pipeline with a prefixed error message
(e.g. `[hook:builder.javascript.before] …`).

## Reloading During Watch
The frontend worker busts the module cache for `webstir.config.js` during `build`
runs, so edits are picked up live while `webstir watch` is running. Publish-mode
runs reuse the cached module for determinism.

## When to Use Hooks
- Inject custom preprocessors (e.g. Markdown to HTML) before the HTML builder.
- Run linters or style checks after CSS transforms.
- Copy additional assets into `build/` or `dist/` without duplicating pipeline
  code.

If you outgrow hooks, you can still fork the TypeScript packages, but most
project-specific tweaks can stay in `webstir.config.js` for easier maintenance.
