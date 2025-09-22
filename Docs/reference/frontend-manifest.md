# Webstir Frontend Manifest

| Field | Description |
| --- | --- |
| `version` | Schema version. Always `1` for the initial rollout. |
| `paths.workspace` | Absolute path to the workspace root sent from the CLI. |
| `paths.src.root` | `src` directory under the workspace. |
| `paths.src.frontend` | `src/frontend` directory housing the app, pages, assets. |
| `paths.src.app` | `src/frontend/app` directory for shared templates and scripts. |
| `paths.src.pages` | `src/frontend/pages` directory containing page-specific assets. |
| `paths.src.images` | `src/frontend/images` source assets. |
| `paths.src.fonts` | `src/frontend/fonts` source assets. |
| `paths.src.media` | `src/frontend/media` source assets. |
| `paths.build.root` | `build` directory root for intermediate artifacts. |
| `paths.build.frontend` | `build/frontend` directory containing compiled assets. |
| `paths.build.app` | `build/frontend/app` directory mirrored from the source app template. |
| `paths.build.pages` | `build/frontend/pages` directory for page-level HTML/JS/CSS artifacts. |
| `paths.build.images` | `build/frontend/images` directory with copied image assets. |
| `paths.build.fonts` | `build/frontend/fonts` directory with copied font assets. |
| `paths.build.media` | `build/frontend/media` directory with copied media assets. |
| `paths.dist.root` | `dist` directory root for publish artifacts. |
| `paths.dist.frontend` | `dist/frontend` directory containing production assets. |
| `paths.dist.app` | `dist/frontend/app` directory for any frontend app-level artifacts. |
| `paths.dist.pages` | `dist/frontend/pages` directory containing page bundles. |
| `paths.dist.images` | `dist/frontend/images` directory containing optimized images. |
| `paths.dist.fonts` | `dist/frontend/fonts` directory containing fonts. |
| `paths.dist.media` | `dist/frontend/media` directory containing media assets. |
| `features.htmlSecurity` | Enables CSP/SRI/transformers when `true`. |
| `features.imageOptimization` | Enables WebP/AVIF generation and sanitization when `true`. |
| `features.precompression` | Enables `.br`/`.gz` precompressed outputs when `true`. |

## Location
The manifest is emitted to:

```
.tools/frontend-manifest.json
```

relative to the workspace root. The TypeScript CLI ensures the `.tools` directory exists and writes the manifest atomically on every `build`, `publish`, or `rebuild` command.

## Purpose
- Serves as the only contract between the TypeScript frontend CLI and the .NET host now that the legacy C# asset handlers are removed.
- Allows the .NET CLI to understand the folder structure without hardcoding string literals.
- Enables feature toggles (image optimization, precompression, hardening) to flow from TypeScript into runtime services.
- Synchronizes the schema between TypeScript and C# via generated typings that can be re-used by the bridge.

All runtime consumers (`FrontendWorker`, `WebServer`, manifest-aware tests) must resolve paths and feature flags through this file instead of the removed `Engine.Pipelines` utilities.

## Validation
The manifest is validated through the shared `zod` schema located at `framework/frontend/src/config/schema.ts`. Any CLI integration should mirror these constraints to fail fast when the contract drifts.
