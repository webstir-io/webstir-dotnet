# Test

Build the project and execute tests through the Webstir test host. By default the host uses the VM-based `@webstir-io/webstir-testing` provider, but you can swap in alternatives such as the Vitest-powered provider without changing project code.

## Purpose
- Validate behavior via end-to-end and smoke tests.
- Keep the public contracts locked down.

## When To Use
- After changes to pipelines or templates.
- In CI to gate merges and releases.

## CLI
- `webstir test`

## Steps
1. Ensure build outputs are up to date (runs `build` if needed).
2. Ensure the recorded `@webstir-io/webstir-testing` package is installed into the workspace (resolved via `webstir install`).
3. Execute compiled frontend and backend tests via the test host and stream structured results back to the .NET bridge.
4. Print pass/fail summary with file names for failures.

## Provider Selection

### Defaults
- `@webstir-io/webstir-testing` (pinned VM runtime) runs compiled tests inside the sandbox that ships with the CLI.
- Provider overrides apply to the entire manifest run; mixed providers per-module are not supported.

### Quickstart: Vitest Provider
1. Install the provider (once per workspace):\
   `npm install --save-dev @webstir-io/vitest-testing`
2. Ensure `vitest` is available (the provider resolves the workspace dependency).
3. Run the suite:\
   `WEBSTIR_TESTING_PROVIDER=@webstir-io/vitest-testing webstir test`
4. For unpublished builds from the standalone repository, set `WEBSTIR_TESTING_PROVIDER_SPEC=<path-to-local-vitest-testing>`, for example `../vitest-testing`, so the host installs your local checkout.

> Tip: Keep `WEBSTIR_TESTING_PROVIDER_SPEC` empty (default) when consuming the published provider from the registry.

### Persist Provider Choices
- Generated workspaces include a `webstir.providers.json` file with the default testing provider. Update it to persist provider selection in source control:

```json
{
  "testing": "@webstir-io/vitest-testing"
}
```

- When `webstir.providers.json` references a provider, run `webstir install` to ensure the dependency is present before executing commands that rely on it.

## Known Limitations
- Snapshot assertions are not yet supported in the default VM provider; track behavior through standard assertions or adopt an alternate provider that supplies its own snapshot tooling.
- Tests execute serially today. Parallel execution toggles are planned but not yet exposed—expect sequential runs until the runtime grows explicit parallel orchestration.
- Registry installs require GitHub Packages credentials while the framework packages remain private. Ensure `GH_PACKAGES_TOKEN` (or `NODE_AUTH_TOKEN`) is available before running `webstir install`/`webstir test` in clean environments.

## Outputs
- Exit code 0 on success; non-zero on failures.
- Logs with clear failure messages.

## Errors & Exit Codes
- Non-zero on test failures, build failures, or configuration errors.

## Related Docs
- Workflows — [workflows](../reference/workflows.md)
- Tests — [tests](../explanations/testing.md)
- Engine — [engine](../explanations/engine.md)
- Pipelines — [pipelines](../explanations/pipelines.md)
