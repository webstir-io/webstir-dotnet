# CI Pipeline Plan

## Goals
- Provide consistent automation for tests, package publishing, and releases.
- Keep contributors fast: local validation mirrors CI, and release publishing is push-button.
- Eliminate manual version bumping to avoid human error.

## Pipelines Overview
1. **Continuous Integration (CI)**
   - Trigger: every push and pull request.
   - Steps:
     1. checkout + package environment setup.
     2. `./utilities/local-ci.sh` equivalent: dotnet build/tests, npm tests, `webstir packages verify`.
     3. Report results (fail PR on any failure).
   - Purpose: fast feedback for contributors.

2. **Release Pipeline**
   - Trigger: manual workflow dispatch or push of a git tag (`v*`).
   - Steps:
     1. checkout at release commit.
     2. compute next semantic version automatically (see section below).
     3. run `webstir packages sync` to regenerate tarballs and manifest.
     4. run `webstir packages publish --version X.Y.Z` to push packages to GitHub Packages.
     5. create git tag + GitHub release, attach tarballs if desired.
     6. optionally push the tag back to origin.
   - Purpose: produce installable artifacts (npm packages) and version the framework.

3. **Nightly/Periodic Validation (optional)**
   - Trigger: scheduled (e.g. daily).
   - Steps: run CI build + `webstir packages verify` using the latest `main` to ensure reproducibility, alert maintainers if hashes drift.

## Automatic Versioning Strategy
- Maintain a version manifest (e.g. `framework/version.json`) or rely on package.json versions.
- Use GitHub Actions workflow to determine the next version by inspecting commit history or tags.
- Proposed scheme: semantic versioning (major.minor.patch) with the following rules:
  - `--bump patch` for bugfix commits (default).
  - `--bump minor` when a PR is labeled `release:minor`.
  - `--bump major` when labeled `release:major`.
- The workflow will:
  1. Detect the bump level based on PR labels or release inputs.
  2. Update `framework/frontend/package.json` and `framework/testing/package.json` automatically.
  3. Commit those bumps (detached release branch) and proceed with package sync/publish.

## Developer Workflow Improvements
- Keep tarballs out of git; developers only run `webstir packages sync` when they modify package sources.
- Provide helper script (`./scripts/sync-packages-if-needed.sh`) that detects changes under `framework/frontend|testing` and runs sync + stages updated manifest when necessary.
- Document: "Normal code changes -> run `./utilities/local-ci.sh`. Only run sync when touching TypeScript packages or preparing a release."

## Next Steps
1. Remove committed tarballs and ignore generated artifacts.
2. Update CLI build/publish commands to support version parameter and skip commit metadata.
3. Implement GitHub Actions release workflow with auto version bump + publish.
4. Add helper script for developers to detect when package sync is needed.
5. Update documentation/how-to guides to reflect the registry-first workflow and release steps.
