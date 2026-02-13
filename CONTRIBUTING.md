# Contributing to Webstir

We love contributions!  
By contributing, you agree that your work is licensed under the project’s MIT License
and may be incorporated into the project owned by **Electric Coding LLC**.

## Quick Start
1. Fork the repo and create a feature branch.  
2. Sign off your commits (`git commit -s`) to certify the Developer Certificate of Origin.  
3. Open a Pull Request describing your change with links to related issues or plans.

## Local Environment
- **Required runtimes**: .NET 10 SDK, Node.js 20.18.x (or newer).  
- **Registry access**: all framework packages are published to npmjs.
  - Create a npm access token with publish access when you need to release packages.
  - Configure `.npmrc` or export `NPM_TOKEN`/`NODE_AUTH_TOKEN`:
    ```ini
    @webstir-io:registry=https://registry.npmjs.org
    //registry.npmjs.org/:_authToken=${NPM_TOKEN}
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
1. Publish framework packages from their owning repos (`webstir-frontend`, `webstir-backend`, `webstir-testing`).
2. Run `Utilities/scripts/sync-framework-versions.sh` to pin released versions and verify.
3. Commit source, lockfiles, and `framework-packages.json`.
4. Trigger the release workflow (clean `main` only): `Utilities/scripts/publish.sh patch`.

## Developer Certificate of Origin
By signing off, you certify that you have the right to submit the code
under the MIT License and that it is your original work.

Signed-off-by: Chris Edwards <chris@electriccoding.com>
