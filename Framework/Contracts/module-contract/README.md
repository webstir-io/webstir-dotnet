# @webstir-io/module-contract

TypeScript interfaces and JSON schema describing Webstir module providers. Frontend/backend packages implement this contract so the Webstir CLI and installers can orchestrate builds consistently.

## Install

```ini
# .npmrc
@webstir-io:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
```

```bash
npm install @webstir-io/module-contract
```

Consumers only need `read:packages`; publishers also require `write:packages`.

## Provided Types

```ts
import type {
  ModuleProvider,
  ModuleProviderMetadata,
  ModuleBuildOptions,
  ModuleBuildResult,
  ModuleBuildManifest,
  ModuleArtifact,
  ModuleAsset,
  ModuleDiagnostic,
  ResolvedModuleWorkspace,
} from '@webstir-io/module-contract';
```

- `ModuleProvider` defines the contract (`metadata`, `resolveWorkspace`, `build`, optional `getScaffoldAssets`).
- `ModuleBuildOptions` conveys workspace location, env vars, and incremental hint.
- `ModuleBuildResult` combines produced `ModuleArtifact`s and the manifest consumed by synchronizers.
- `ModuleDiagnostic` captures structured warnings/errors surfaced during builds.

The package also ships `schema/ModuleProvider.schema.json` for non-TypeScript consumers. It mirrors the TypeScript declarations and enables runtime validation.

## Usage Example

```ts
import type { ModuleProvider } from '@webstir-io/module-contract';
import { frontendProvider } from '@webstir-io/webstir-frontend';

async function build(workspaceRoot: string, provider: ModuleProvider) {
  const result = await provider.build({ workspaceRoot, env: {} });
  return result.manifest.entryPoints;
}
```

When authoring a provider:

1. Populate `metadata` with id, version, and CLI compatibility info.
2. Return absolute filesystem paths in `ModuleArtifact.path`.
3. Emit `ModuleDiagnostic`s for recoverable issues.
4. Use `ResolvedModuleWorkspace` to centralize workspace path resolution.

## Maintainer Workflow

```bash
npm install
npm run build          # emits dist/index.js, dist/index.d.ts, and schema/
```

- Regenerate `schema/ModuleProvider.schema.json` whenever typings change.
- Ensure CI runs lint/build and fails on schema drift before publish.

## License

MIT © Webstir
