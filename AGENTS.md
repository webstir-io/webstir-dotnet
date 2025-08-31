# Repository Guidelines

## Assistant Pre‑Flight Checklist
- Always consult `.codex/Codex-Patterns.md` before planning or making non‑trivial changes.
- Prefer the docs under `Docs/framework-pipeline-codex/` for current architecture and feature plans.
- Follow YAGNI, explicit types, fail‑fast error handling, and framework‑agnostic constraints (no React/Next, no JSX/CJS in v1).
- Keep changes minimal and localized; avoid introducing flags unless requested.

## Project Structure & Module Organization
- `CLI/`: Console app entrypoint (`webstir`), help, argument parsing.
- `Engine/`: Core build/watch/publish engine, pipelines for HTML/CSS/JS, servers, services, workflows, and embedded `Templates/` (seed project files).
- `Tests/`: Custom test runner and suites under `Tests/Suite/*Tests.cs`.
- `Docs/`: Design notes and plans; non-normative for builds.
- Solution: `Webstir.sln` ties projects; outputs: `build/` (dev) and `dist/` (prod) in target projects.

## Build, Test, and Development Commands
- Build solution: `dotnet build Webstir.sln` — restores and compiles all projects.
- Run CLI locally: `dotnet run --project CLI -- watch` — starts dev server with rebuild on change.
  - Other commands: `init`, `add-page <name>`, `build [--clean]`, `publish`, `help [command]`, `demo [dir]`.
  - Example: `dotnet run --project CLI -- build --clean`.
- Run tests: `dotnet run --project Tests` — executes all suites.
  - Single suite: `dotnet run --project Tests -- test build`.

## Coding Style & Naming Conventions
- Language: C# (net9.0). Indentation: 4 spaces; UTF-8; LF line endings.
- Naming: PascalCase for types/methods; camelCase for locals/params; constants in `Constants.cs` use PascalCase.
- Files: one public type per file; filename matches type (e.g., `WatchService.cs`).
- Namespaces: `Engine.*`, `CLI`, `Tests.*`. Keep methods small and focused; prefer pure helpers under `Engine/Helpers`.

## Testing Guidelines
- Framework: custom lightweight runner in `Tests/Framework` (no xUnit/NUnit).
- Suites: add a `*Tests.cs` class in `Tests/Suite`, derive from `BaseTest`, override `Name` and `RunAsync()`.
- Conventions: test names describe behavior; use `Assert.*` helpers; prefer `RunCliCommand(...)` for end‑to‑end checks.

## Commit & Pull Request Guidelines
- Commits: short, imperative, one idea (e.g., "Add Html builder and bundler").
- PRs: clear description, link issues, list commands tested, include relevant output snippets (build logs, tree of `build/` or `dist/`).
- CI: none yet; ensure `dotnet build` and `dotnet run --project Tests` pass locally.

## Security & Configuration
- Optional `CLI/appsettings.json` is copied to output; environment variables are supported. Do not commit secrets.
- Generated template projects include `node_modules/`; avoid committing it. Use `--clean` to reset build artifacts.
