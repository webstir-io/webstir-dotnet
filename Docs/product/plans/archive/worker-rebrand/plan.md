# Worker Rebrand Plan (Client → Frontend, Server → Backend)

Goal: Rename all "Client" to "Frontend" and "Server" to "Backend" across code, templates, tests, and docs. This is a clean rename with no legacy aliases.

## Decisions
- No aliases or legacy support (no `src/client` or `src/server` accepted).
- No shim classes/constants; all names will be updated in place.
- Keep `WebServer` and `NodeServer` class/file names unchanged.

## Code Changes (C#)
- Update `Engine/Constants.cs`
  - Rename `Folders.Client` → `Folders.Frontend`.
  - Rename `Folders.Server` → `Folders.Backend`.
  - Update `ClientPath`/`ServerPath` constants accordingly (rename to `Frontend*`/`Backend*`).

- Update `Engine/AppWorkspace.cs`
  - Rename properties: `ClientPath`, `ClientBuildPath`, `ClientDistPath` → `FrontendPath`, `FrontendBuildPath`, `FrontendDistPath`.
  - Rename `ServerPath`, `ServerBuildPath`, `ServerDistPath` → `BackendPath`, `BackendBuildPath`, `BackendDistPath`.
  - Update any logic that references `Folders.Client`/`Folders.Server`.

- Rename workers and references
  - File/class: `Engine/Workers/ClientWorker.cs` → `FrontendWorker.cs` (class `FrontendWorker`).
  - File/class: `Engine/Workers/ServerWorker.cs` → `BackendWorker.cs` (class `BackendWorker`).
  - Update all usages and DI/wiring.

- Update servers and logging (names unchanged)
  - `Engine/Servers/WebServer.cs`: update paths/messages (Client → Frontend) without renaming `WebServer`.
  - `Engine/Servers/NodeServer.cs`: update paths/messages (Server → Backend) without renaming `NodeServer`.
  - `Engine/Services/ChangeService.cs`: update messages and folder checks.

- Update pipelines/handlers that use client/server folders
  - Search for `Folders.Client` and `Folders.Server` usages across handlers/pipelines.
  - Update any path computations, especially build/dist paths.

- CLI/Program and options
  - Check for flags or help text that mention "client"/"server".
  - Update help text and defaults; remove or replace options using client/server terms (e.g., `--frontend-only`).

## Templates & Seeds
- Move template folders
  - `Engine/Templates/src/client/**` → `Engine/Templates/src/frontend/**`.
  - Any `src/server/**` → `src/backend/**`.
  - Update internal import paths and comments.

- Update dev scripts and runtime glue
  - Change any references inside template files to new paths (e.g., refresh scripts, entry points).
  - Ensure server entry now `src/backend/index.ts` compiles to `build/backend/index.js`.

## Tests
- Update path assertions
  - Replace `Folders.Client` → `Folders.Frontend` and `Folders.Server` → `Folders.Backend`.
  - Update hardcoded paths in tests (e.g., `src/client`, `build/client`, `dist/client`).

- Update seed/build fixture outputs
  - If test fixtures check built files under `build/client` or `dist/client`, update to `build/frontend` / `dist/frontend`.

## Documentation
- README
  - Update project tree to show `frontend/` and `backend/`.
  - Update references to dev server behavior and entries.

- `docs/reference/cli.md`
  - Replace terminology and examples: client → frontend, server → backend.
  - Update flags and usage examples (`--frontend-only` etc.).

- `docs/reference/templates.md`
  - Update folders and entry points to `src/frontend/**` and `src/backend/index.ts`.

- `docs/reference/contracts.md`
  - Update contract paths and dev server descriptions.

- Add migration guide
  - Keep it concise: mapping tables only; no deprecation path since no legacy support.

## Tooling & Config
- VS Code launch/tasks
  - Update `.vscode/launch.json` and `.vscode/tasks.json` if they reference client/server paths.

- Any hardcoded glob patterns
  - Search for `src/client` and `src/server` in configs, scripts, and docs.

## Communication
- No release notes needed; treat as an internal rename.

## Execution Checklist (ordered)
1) Update `Engine/Constants.cs` (rename Client/Server to Frontend/Backend).
2) Update `Engine/AppWorkspace.cs` properties and usages.
3) Rename workers and update all references.
4) Update servers/services log messages and folder checks.
5) Update handlers/pipelines referencing client/server folders.
6) Update CLI help/flags (remove old terms, add replacements).
7) Move/rename template folders and fix internal paths.
8) Update tests (paths, constants, fixtures outputs).
9) Update docs (README, reference docs).
10) Update docs and examples.
11) Run build/tests; fix any stragglers.

## Search Queries To Drive Changes
- Code: `Folders.Client|Folders.Server`, `ClientPath|ServerPath`, `ClientBuildPath|ServerBuildPath`, `ClientDistPath|ServerDistPath`.
- Files/classes: `ClientWorker|ServerWorker`.
- Paths: `src/client|build/client|dist/client|src/server|build/server|dist/server`.
- Docs: `Client\b|Server\b`.
