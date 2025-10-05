# Webstir — Official Product Readiness Plan

Purpose: a practical checklist and narrative to make Webstir feel “official” to new users, contributors, and reviewers. Use it to drive a short series of PRs until everything here is green.

## Positioning & Story
- One‑liner: Zero‑config, reproducible, HTML‑first TypeScript sites with a single binary.
- Who it’s for: SSGs/docs/marketing sites and lightweight full‑stack apps.
- Non‑goals: No SSR/ISR, no plugin bazaar; hooks for extension instead.
- Differentiators: Self‑contained CLI + signed binaries, deterministic outputs, typed TS↔.NET manifest boundary, fast esbuild/tsc pipeline.
- Actions
  - Add “Why Webstir” to `README.md` (value, non‑goals, who/when to choose it).
  - Add small architecture diagram (host ↔ frontend CLI ↔ dev/proxy servers).

## Licensing & Governance
- Add `LICENSE` (MIT or Apache-2.0; pick and stick to it).
- Add `CODE_OF_CONDUCT.md` and `CONTRIBUTING.md` (setup, PR process, local run).
- Add `SECURITY.md` (how to report vulnerabilities, supported versions).
- Optional: `CODEOWNERS` (review routing), CLA policy (if needed later).
- Transfer the repository into a dedicated GitHub organization while preserving existing commit authorship and maintainer attribution.

## Versioning, Releases, and Artifacts
- SemVer policy (major/minor/patch; what constitutes a breaking change).
- Changelog: human‑curated `CHANGELOG.md` per release.
- Signed, reproducible binaries for macOS arm64/x64, Linux x64/arm64, Windows x64.
- Release manifest: `install-manifest.json` with version/platform/URLs/checksums.
- Signature/verification: `cosign` or `minisign` public keys published in README.
- Actions
  - Finish the release pipeline already scaffolded in `.github/workflows/release.yml`.
  - Publish the release manifest + attached binaries; document verification commands.
  - Add README badges (CI, Release) and direct links to the latest binaries.

## Distribution Channels (One‑Command Installs)
- Homebrew tap (`electric/webstir`) with `sha256` validation and a `test do` block.
- npm wrapper (`@electric-coding/webstir`) with postinstall binary fetch + checksum.
- Curl installer (`install.sh`) with `--version` + `--install-dir` and signature checks.
- Windows: winget + Scoop manifests kept in sync from the release manifest.
- Version managers: `asdf` (and optionally `mise`) plugin delegating to curl installer.
- Actions
  - Implement each channel to consume `install-manifest.json` to avoid drift.
  - Document all channels at the top of `README.md` (copy‑paste commands).

## Installation & Onboarding
- Prereqs: .NET 9, Node 20.18+, `tsc` on PATH (document quick commands).
- Post‑install: `webstir install` to pin embedded tarballs and stay offline‑friendly.
- First 5 minutes: `webstir init --client-only my-site && webstir watch`.
- Actions
  - Ensure help text and README make `webstir install` explicit after upgrades.

## Documentation IA
- Overview index with “Why Webstir”, Concepts, and Non‑Goals.
- Quick Start, Tutorials, How‑to (task‑based), Reference (CLI/manifest/contracts), Explanations (architecture/pipelines/servers/testing).
- Content pipeline example: official `webstir.config.js` Markdown hook mapping `content/**/*.md → src/frontend/pages/<slug>/index.html|css|ts` with frontmatter validation.
- Actions
  - Add a how‑to: “Build an SSG with Markdown hooks” and link from README + docs index.
  - Cross‑link `docs/reference/frontend-manifest.md` wherever the C#↔TS boundary appears.

## Examples & Starters
- `examples/client-only-ssg/` — Markdown hook starter with a couple of posts.
- `examples/fullstack-api/` — minimal API round‑trip with `/api/health` and a page that calls it.
- Sandbox: keep `Sandbox/docker-compose.yml` path in the docs with a GIF of live reload.

## CI, Quality, and Security
- CI matrix: Linux/macOS/Windows runners; format, build, tests (frontend + .NET), package verification.
- Green shields in README; required checks for PR merge.
- Dependabot (GitHub Actions + npm) or Renovate; lockfile maintenance.
- Security: `SECURITY.md`, vulnerability scanning (e.g., `npm audit` gate for wrappers), signed releases.
- Reproducibility: embed version info, checksums verification in installers, pinned tarballs via `webstir install`.
- Actions
  - Fix case‑sensitive paths in CI/release workflows (`Framework/*` vs `framework/*`).
  - Add Dependabot config for actions and npm.

## Product Hardening (Deploy Reality)
- Root‑absolute assets: document subpath deploy strategy (rewrite on publish or CDN rule) for GitHub Pages‑style hosting.
- Accessibility and performance defaults: ensure publish minifies, fingerprints, and precompresses HTML/CSS/JS (already in place); encourage image optimization.
- Telemetry: if ever added, keep it opt‑in with a clear policy.

## Community & Support
- GitHub Discussions (Q&A, ideas) and labeled issues (good first issue, help wanted).
- Issue/PR templates (bug, feature, docs) that collect the right signals.
- SLA expectations for maintainers (triage weekly; releases as needed).

---

## Checklists

### Maintainer Readiness
- [ ] LICENSE added and referenced in package metadata.
- [ ] CODE_OF_CONDUCT.md, CONTRIBUTING.md, SECURITY.md, CODEOWNERS present.
- [ ] CI green on Linux/macOS/Windows; required checks enforced.
- [ ] `README.md` has badges, Why Webstir, install one‑liners, quick start.
- [ ] Architecture diagram embedded in README and `docs/explanations/solution.md`.
- [ ] Examples: client‑only SSG + full‑stack API.
- [ ] Docs: official Markdown hook how‑to; manifest and pipelines clearly linked.
- [ ] Release: signed binaries + install manifest attached; verification doc in README.
- [ ] Distribution: Homebrew, npm wrapper, curl installer, winget/Scoop live.
- [ ] Dependabot (actions + npm) configured; lockfiles current.

### Release Playbook
1) Bump version per SemVer; update CHANGELOG.
2) Build signed binaries; generate `install-manifest.json` (version/platform/URLs/checksums).
3) Publish GH release with artifacts + manifest + signatures.
4) Update distribution channels from the manifest (brew tap, npm wrapper, winget/Scoop).
5) Verify installs on clean machines/containers (macOS, Linux, Windows).
6) Announce release; update docs for any behavioral changes.

### Subpath Deploy (If Needed)
- [ ] Add publish‑time path rewriter or CDN rule to replace `src="/..."` with `src="/basepath/..."`.
- [ ] Document the deploy recipe for GitHub Pages project sites.

---

## Near‑Term Action Items
1) Fix CI workflow case mismatches (`Framework/*` vs `framework/*`).
2) Add LICENSE, CoC, Contributing, Security, templates under `.github/`.
3) Add “Why Webstir”, badges, and install one‑liners to `README.md`.
4) Publish the official Markdown hook how‑to and link it prominently.
5) Prepare a first binary release with manifest + signatures; wire Homebrew/npm wrapper.

## References
- Installation & distribution design: `docs/product/framework/installation.md`
- Frontend manifest (TS↔.NET contract): `docs/reference/frontend-manifest.md`
- CLI reference: `docs/reference/cli.md`
- Pipelines & hooks: `docs/explanations/pipelines.md`, `docs/how-to/pipeline-hooks.md`
