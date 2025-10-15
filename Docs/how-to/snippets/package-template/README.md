# Package Template Snippet

Use this snippet as a starting point when adding a new framework package under `Framework/`.

1. Copy the contents of this directory into `Framework/<PackageName>/`.
2. Replace `@webstir-io/package-template` with the real package name and update the description.
3. Flesh out the `build`, `test`, and `pack` scripts so they produce deterministic tarballs that match the framework pipeline.
4. Remove placeholder `TODO` comments before committing the new package.

Keep the scaffold minimal—additional build tooling should live inside the package itself so the CLI can invoke it without special handling.
