# Contributing to Webstir

We love contributions!  
By contributing, you agree that your work is licensed under the project’s MIT License
and may be incorporated into the project owned by **Electric Coding LLC**.

## Quick Start
1. Fork the repo and create a feature branch.  
2. Sign off your commits (`git commit -s`) to certify the Developer Certificate of Origin.  
3. Open a Pull Request describing your change with links to related issues or plans.

## Local Environment
- **Required runtimes**: .NET 9 SDK, Node.js 20.18.x (or newer).  
- **Registry access**: all framework packages are published to GitHub Packages.  
  - Create a classic personal access token with `read:packages` (and `write:packages` if you publish).  
  - Configure `.npmrc` or export `GH_PACKAGES_TOKEN`/`NODE_AUTH_TOKEN`:
    ```ini
    @webstir-io:registry=https://npm.pkg.github.com
    //npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
    ```

## Common Tasks
| Task | Command |
|------|---------|
| Install framework dependencies | `npm ci --prefix Framework/Frontend`<br>`npm ci --prefix Framework/Testing` |
| Restore solution & packages | `dotnet build Webstir.sln -v minimal` |
| Run workflow tests (quick) | `dotnet test Tester/Tester.csproj` |
| Run full workflow tests | `WEBSTIR_TEST_MODE=full dotnet test Tester/Tester.csproj` |
| Format & build sanity check | `./utilities/scripts/format-build.sh` |
| Rebuild & verify framework packages | `dotnet run --project Framework/Framework.csproj -- packages sync`<br>`dotnet run --project Framework/Framework.csproj -- packages verify` |

> Tip: `./utilities/scripts/local-ci.sh` builds the Docker image used by CI and runs the same workflow (npm installs, dotnet build/test, framework package sync/verify) against your checkout.

### CI Lanes (Quick vs Full)
- PRs run the Quick lane by default (fast unit/integration set).
- Full runs on `main` and on PRs labeled `ci:full`.
  - Add the `ci:full` label to your PR to trigger the Full lane.
  - Full includes native image tooling setup (sharp) and package sync/verify.

## Tests & Linting
- Favor the end-to-end workflow tests in `Tester/` when changing CLI behavior.  
- Frontend package tests run via `npm test --prefix Framework/Frontend`.  
- Keep TypeScript/JavaScript changes formatted via `npm run lint`/`npm run format` when available; `format-build.sh` covers the common cases.

## Release Workflow (Maintainers)
1. Update package sources under `Framework/*`.
2. Run `framework packages bump` / `framework packages sync` / `framework packages verify`.
3. Commit source, lockfiles, and `framework-packages.json`.
4. Trigger the release workflow (or run `framework packages publish`) with `GH_PACKAGES_TOKEN` set.

## Developer Certificate of Origin
By signing off, you certify that you have the right to submit the code
under the MIT License and that it is your original work.

Signed-off-by: Chris Edwards <chris@electriccoding.com>
