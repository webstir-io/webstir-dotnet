# Add Page

Scaffold a new frontend page with `index.html|css|ts` under `src/frontend/pages/<name>/`.

## Purpose
- Create a new routed page quickly with the expected files.
- Ensure the page follows conventions used by build and publish.

## When To Use
- Adding a new top-level page in the app.

## CLI
- `webstir add-page <name>`

## Notes
- Frontend only: this command scaffolds files under `src/frontend/pages/` and does not touch backend or shared code.
- Internals: the CLI shells into the `@webstir/frontend` TypeScript package (`webstir-frontend add-page`) so scaffolding stays in sync with the framework templates.

## Inputs
- `<name>`: normalized and validated page name. If the page already exists, the workflow fails.

## Steps
1. Validate `<name>` and resolve `src/frontend/pages/<name>/`.
2. Delegate to `webstir-frontend add-page` to create `index.html`, `index.css`, and `index.ts` from the framework templates.
3. The TypeScript CLI updates intrinsics (imports, references) so the page builds immediately.

## Outputs
- New page folder and files under `src/frontend/pages/<name>/`.
- Picked up automatically by `build`, `watch`, and `publish`.

## Errors & Exit Codes
- Non-zero if the page exists, the name is invalid, or file IO fails.

## Related Docs
- Workflows — [workflows](../reference/workflows.md)
- Build — [build](build.md)
- Watch — [watch](watch.md)
- Publish — [publish](publish.md)
- Workspace — [workspace](../explanations/workspace.md)
