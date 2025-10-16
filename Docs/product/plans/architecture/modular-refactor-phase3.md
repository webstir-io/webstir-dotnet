# Modular Refactor Phase 3 — Alternate Provider Pilot

## Goals
- Validate that the adapter contracts support real-world swaps for testing, frontend, and backend modules.
- Exercise the bridge infrastructure end-to-end (CLI ↔ provider ↔ .NET host) and surface any schema or workflow gaps.
- Define graduation criteria for providers to become “supported” vs. “experimental”.

## Candidate Providers

### Testing
- **Vitest adapter** — Runs compiled JS tests with rich diagnostics and watch support.
- Stretch: **Playwright test adapter** for end-to-end flows once the Vitest pilot lands.

### Frontend
- **Vite provider** — Run the default frontend pipeline through Vite (current pilot target, available via `@webstir-io/webstir-frontend-vite`).
- Optional: **React/Vite provider** to layer component frameworks after the Vite pilot lands.
- Optional: **Next.js provider** to validate SSR pathways later.

### Backend
- **Express + ts-node provider** — Compiles TypeScript backend assets and exports an HTTP entry point.
- Optional: **Fastify provider** as a lightweight alternative.

## Evaluation Criteria
- Contract compliance (`npm run validate:contracts`, schema validation).
- CLI integration: `webstir build`, `webstir publish`, `webstir watch`, and `webstir test` succeed without tweaking host code.
- Diagnostics parity: failures and warnings surface through provider manifests and host logs.
- Performance and stability: compare runtime and failure rates against the default providers.
- Provider selection surfaced via environment variables (`WEBSTIR_FRONTEND_PROVIDER`, `WEBSTIR_BACKEND_PROVIDER`) or workspace config.

## Pilot Timeline
1. **Prototype (Week 1-2)**
   - Implement Vitest provider + React frontend provider in feature branches.
   - Add smoke workspaces to exercise the providers.

2. **Integration (Week 3)**
   - Wire provider selection via config (`webstir.config.ts` or package.json field).
   - Update host workflows to respect configured providers per workspace.

3. **Validation (Week 4)**
   - Run full `webstir init/install/build/publish/test` flows with providers enabled.
   - Document gaps and required contract changes; feed back to ADR 0001 if needed.

4. **Pilot Review (Week 5)**
   - Record findings, decide support level (supported/experimental/community).
   - Update documentation/guides based on pilot experiences.

## Requirements for Provider Authors
- Publish package implementing `@webstir-io/testing-contract` or `@webstir-io/module-contract`.
- Ship `npm run build` + `npm run validate:contracts` commands.
- Include documentation: configuration, prerequisites, known limitations.
- Provide integration tests or sample workspaces to aid regression testing.

## Open Questions
- How to expose provider selection to users (CLI flags, workspace config, environment variables)?
- Should we offer a “provider matrix” package that pins compatible versions (frontend + backend + testing)?
- How do we sandbox user-provided providers to avoid executing arbitrary scripts during `webstir` commands?
- What telemetry or logging do we need to monitor provider adoption?
