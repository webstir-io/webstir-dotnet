# Add Page

Scaffold a new client page with `index.html|css|ts` under `src/client/pages/<name>/`.

## Purpose
- Create a new routed page quickly with the expected files.
- Ensure the page follows conventions used by build and publish.

## When To Use
- Adding a new top‑level page in the app.

## CLI
- `webstir add-page <name>`

## Inputs
- `<name>`: normalized and validated page name. If the page already exists, the workflow fails.

## Steps
1. Validate `<name>` and resolve `src/client/pages/<name>/`.
2. Create the folder and write `index.html`, `index.css`, and `index.ts` from templates.
3. Ensure references are compatible with `app.html` and client pipelines.

## Outputs
- New page folder and files under `src/client/pages/<name>/`.
- Picked up automatically by `build`, `watch`, and `publish`.

## Errors & Exit Codes
- Non‑zero if the page exists, the name is invalid, or file IO fails.

## Related Docs
- Workflows — [workflows](../workflows.md)
- Build — [build](../build/build.md)
- Watch — [watch](../watch/watch.md)
- Publish — [publish](../publish/publish.md)
- Workspace — [workspace](../../solution/workspace/workspace.md)
