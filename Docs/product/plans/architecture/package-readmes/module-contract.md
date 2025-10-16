# @webstir-io/module-contract

TypeScript interfaces and JSON schema describing Webstir module providers. External frontend/backend providers implement this contract so the CLI and installers can orchestrate builds consistently.

---

## Install

Published to GitHub Packages:

```ini
# .npmrc
@webstir-io:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
```

```bash
npm install @webstir-io/module-contract
```

Consumers only need `read:packages`. Repository publishers require `write:packages`.

---

## Exports

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

- `ModuleProvider` describes the shape packages must implement (`metadata`, `resolveWorkspace`, `build`, optional `getScaffoldAssets`).
- `ModuleBuildOptions` conveys workspace location, environment variables, and `incremental` hint.
- `ModuleBuildResult` combines resolved `ModuleArtifact`s and the `ModuleBuildManifest` consumed by synchronizers.
- `ModuleDiagnostic` encodes structured warnings/errors surfaced during builds.

The published tarball also includes `schema/ModuleProvider.schema.json` for non-TypeScript consumers. It aligns with the TypeScript definitions and can be used for runtime validation or documentation tooling.

---

## Usage Pattern

Packages such as `@webstir-io/webstir-frontend` export their provider:

```ts
import type { ModuleProvider } from '@webstir-io/module-contract';
import { frontendProvider } from '@webstir-io/webstir-frontend';

async function install(workspaceRoot: string, provider: ModuleProvider): Promise<void> {
  const { manifest } = await provider.build({ workspaceRoot, env: {} });
  console.info(manifest.entryPoints);
}
```

When integrating new providers:

1. Import the shared contract to type-check `metadata`, `resolveWorkspace`, `build`, and `getScaffoldAssets`.
2. Emit `ModuleDiagnostic`s for recoverable issues so downstream tooling can surface them.
3. Return absolute paths in `ModuleArtifact.path`; the synchronizer resolves relative destinations.
4. Populate `metadata.compatibility` with the minimum CLI version and required Node.js range.

---

## Maintainer Notes

```bash
npm install
npm run build          # emits dist/index.js and dist/index.d.ts
```

- Regenerate `schema/ModuleProvider.schema.json` whenever the TypeScript types change (`npm run build` handles the emit if the build pipeline includes schema generation).
- Update the README in the dedicated repository prior to publishing.
- Ensure GitHub Actions validate schema drift and package integrity before release.

---

## License

MIT © Webstir
