# Requirements: Scaffolding & Templates

- Scope: `init` project scaffolding, page generator, and test generator using built-in templates.

## Customer Requirements (CR)
- Create a new project with one command and start coding immediately.
- Clean, predictable folder structure with minimal boilerplate.
- Add a page or a test with one command.

## Product Requirements (PR)
- `init <dir>`: scaffold client, server, and shared folders; include tsconfigs and package scripts.
- Default home page, styles, and a simple server with a health endpoint.
- `add page <name>`: create page folder with `index.html`, `index.ts`, and `index.css`.
- `add test <name>`: create a TS test file under the page.
- Do not overwrite existing files without an explicit flag (future); be idempotent when target exists.

## Software Requirements (SR)
- Copy files from `Engine/Templates/**` and apply minimal substitutions.
- Ensure directories exist before writes and preserve relative structure.
- Validate page/test names and locations.
- Report created files; warn on conflicts without overwriting.

## Acceptance Criteria
- After `init`, the project contains client/server/shared folders and compiles.
- `add page home` creates expected files under `src/client/pages/home/`.
- `add test home` creates a test file under `src/client/pages/home/tests/`.
- No existing files are overwritten silently.

