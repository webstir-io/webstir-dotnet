# Webstir Backend Package (Stub)

This directory reserves the namespace for the future `@webstir-io/webstir-backend` package. The package is currently disabled in the release automation:

- Manifest and lockfile exist so the metadata service can discover the package.
- Build and publish scripts are placeholders; the CLI skips the backend while the `isEnabled` flag remains `false`.

When backend automation work begins:

1. Replace the placeholder npm scripts with real build, test, and pack commands.
2. Populate the package sources (`src/`) and ensure `npm run build` emits the distributable artifacts.
3. Update `Framework/Packaging/framework-packages.json`, `FrameworkPackageDescriptor`, and the release automation to enable the package.
