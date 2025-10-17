# Phase 2 Prep — Package Inventory & History Plan

## Package Roots

| Package | Directory | Notes |
|---------|-----------|-------|
| `@webstir-io/webstir-frontend` | `Framework/Frontend` | `src/` TypeScript sources, `dist/` build artifacts, package scripts. Depends on contracts and shared assets under `Framework/Resources`. |
| `@webstir-io/webstir-backend` | `Framework/Backend` | Provider and supporting build logic; TS sources in `src/`, compiled output in `dist/`. Currently version `0.0.0`. |
| `@webstir-io/webstir-test` | `Framework/Testing` | CLI, discovery, runtime. Uses contracts via relative import. |
| `@webstir-io/testing-contract` | `Framework/Contracts/testing-contract` | Source TypeScript definitions and generated schema artifacts. |
| `@webstir-io/module-contract` | `Framework/Contracts/module-contract` | Module provider contract definitions + schema. |
| `@webstir-io/webstir-frontend-vite` (pilot) | `Framework/ViteFrontend` | Will likely stay but not in initial externalization scope; note for follow-up once default packages ship. |

Shared supporting paths:

- `Framework/Resources/webstir/*.tgz` — embedded tarballs (to be deleted post externalization).
- `Docs/how-to/*` — documentation referencing tarball workflow; list for update.
- `utilities/format-build.sh`, `publish.sh`, `.github/workflows/` — scripts referencing tarball packaging.

## History Extraction (draft commands)

The following commands capture each package history via `git subtree split` (can be replaced with `git filter-repo` if better fidelity is required):

```bash
# Frontend
git subtree split --prefix=Framework/Frontend --branch extract/frontend

# Backend
git subtree split --prefix=Framework/Backend --branch extract/backend

# Testing
git subtree split --prefix=Framework/Testing --branch extract/testing

# Testing contract
git subtree split --prefix=Framework/Contracts/testing-contract --branch extract/testing-contract

# Module contract
git subtree split --prefix=Framework/Contracts/module-contract --branch extract/module-contract
```

After extracting, push to the new repository:

```bash
git checkout extract/frontend
git remote add frontend-origin git@github.com:electric/webstir-frontend.git
git push frontend-origin extract/frontend:main
```

Repeat for each package repository using the appropriate branch names.

## Repo Template Checklist

Each new repository should include:

- MIT license, README with usage/install instructions, CHANGELOG stub.
- `.editorconfig`, ESLint/Prettier or equivalent configuration, shared TS config (`tsconfig.base.json`).
- GitHub Actions:
  - `ci.yml` — `npm ci`, lint, tests, build.
  - `release.yml` — version bump detection, tests, publish to GitHub Packages, provenance/SBOM.
- `.npmrc` guidance for maintainers (document required tokens).
- CODEOWNERS mirroring current package owners.
- README drafts live under `Docs/product/plans/architecture/package-readmes/` (`webstir-frontend.md`, `webstir-backend.md`, `webstir-test.md`, `module-contract.md`, `testing-contract.md` ready). Copy each into its matching repository README before publishing.
- `utilities/local-ci-docker.sh` now mounts sibling package repositories; place the split repos alongside the mono-repo (`../webstir-frontend`, etc.) to run their CI workflows locally against GitHub Packages.

## Outstanding Questions

- Determine whether to migrate `Framework/ViteFrontend` alongside core packages or defer until the pilot graduates.
- Confirm dependency boundaries (e.g., shared utilities under `Framework/Services` or `Framework/Packaging`) that might need duplication or extraction into a shared repo.

## Mono-Repo Transition (execution order draft)

1. **Dependency Spec Update**
   - Replace `file:.webstir/*.tgz` references in root `package.json` and any workspace package.json files with registry semver ranges.
   - Add a lock manifest (likely `Framework/Packaging/framework-packages.json`) that records the resolved registry versions.
2. **Installer Refactor**
   - Update `FrontendPackageInstaller`, `BackendPackageInstaller`, `TestPackageInstaller` to accept the desired spec from the manifest and install via registry.
   - Adjust `PackageSynchronizer` to compare installed versions to the manifest instead of tarball hashes; update log messaging.
3. **Script Cleanup**
   - Remove tarball copy/archive code from `Framework/Packaging/PackageBuilder`, `publish.sh`, and CI workflows.
   - Update `utilities/format-build.sh` and related scripts to stop expecting `.webstir/*.tgz`.
4. **Validation & CI**
   - Extend existing tests to cover registry installs (mock via `npm pack` or local Verdaccio).
   - Update GitHub Actions to consume published packages during verification steps.

## Repository Status (2025-03)

| Repository | URL | History migrated? | Notes |
|------------|-----|-------------------|-------|
| `webstir-frontend` | https://github.com/webstir-io/webstir-frontend | ✅ | Contains former `Framework/Frontend` history via subtree split. |
| `webstir-backend` | https://github.com/webstir-io/webstir-backend | ✅ | Contains former `Framework/Backend` history. |
| `webstir-test` | https://github.com/webstir-io/webstir-test | ✅ | Contains former `Framework/Testing` history. |
| `testing-contract` | https://github.com/webstir-io/testing-contract | ✅ | Contains `Framework/Contracts/testing-contract` history. |
| `module-contract` | https://github.com/webstir-io/module-contract | ✅ | Contains `Framework/Contracts/module-contract` history. |

Next steps for each repo: add README/license, stand up CI (`ci.yml`, `release.yml`), and wire secrets for registry publish.
