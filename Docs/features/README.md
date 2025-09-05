# Features

Central index for feature docs. Each feature set has a hub doc that describes scope, links to sub‑feature requirements, and lists prioritized product requirements. We use RBDS (Requirements Based Development System) to organize work from customer needs to software requirements.

See also: `docs/mission.md`, `docs/rbds.md`, and the aggregated `docs/backlog.md`.

## Feature Sets

- Bundling: Build graph, transforms, dev server, outputs — `docs/features/bundling/bundling.md`
- Framework: App conventions and runtime (routing, data, SSR/SSG, deployment) — `docs/features/framework/framework.md`
- Dev Service: Orchestrates (watch, changes, server lifecycle) — `docs/features/devservice/devservice.md`
- Pipelines: HTML, CSS, JS processing stages and contracts — `docs/features/pipelines/pipelines.md`
- Solution: CLI and host tooling — `docs/features/solution/solution.md`
- Templates: Scaffolding and code generators — `docs/features/templates/templates.md`
- Testing: Philosophy and workflow coverage — `docs/features/testing/testing.md`
- Workflows: End‑to‑end flows the tool supports — `docs/features/workflows/workflows.md`
- Workspace: Project roots, constants, and paths — `docs/features/workspace/workspace.md`

## How We Work

- Start in a hub doc to understand scope and find sub‑feature `requirements.md` files.
- Prioritize product requirements in the hub doc; the central backlog aggregates them in `docs/backlog.md`.
- When adding a feature: create `requirements.md` under its folder, link it from the hub doc, and update the backlog.
