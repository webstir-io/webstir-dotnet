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
2. Ensure the embedded `@webstir-io/webstir-testing` package is installed into the workspace.
3. Execute compiled frontend and backend tests via the test host and stream structured results back to the .NET bridge.
4. Print pass/fail summary with file names for failures.

## Provider Selection

### Defaults
- `@webstir-io/webstir-testing` (bundled VM runtime) runs compiled tests inside the sandbox that ships with the CLI.
- Provider overrides apply to the entire manifest run; mixed providers per-module are not supported.

### Quickstart: Vitest Provider
1. Install the provider (once per workspace):\
   `npm install --save-dev @webstir-io/vitest-testing`
2. Ensure `vitest` is available (the provider resolves the workspace dependency).
3. Run the suite:\
   `WEBSTIR_TESTING_PROVIDER=@webstir-io/vitest-testing webstir test`
4. For unpublished builds from the standalone repository, set `WEBSTIR_TESTING_PROVIDER_SPEC=<path-to-local-vitest-testing>`, for example `../vitest-testing`, so the host installs your local checkout.

> Tip: Keep `WEBSTIR_TESTING_PROVIDER_SPEC` empty (default) when consuming the published provider from the registry.

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
