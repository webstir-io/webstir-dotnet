# Instructions

- KISS
- YAGNI, but ensure solutions are easy to evolve
- DRY
- SOLID
- Avoid excessive comments; code should be self-documenting whenever possible
- Strive for elegant code: simple, readable, consistent, concise, and easy to evolve
- Code must be readable, maintainable and easily debuggable.
- Use straightforward, concise language. Avoid jargon and marketing-speak.

# Agent Application

- Apply the repository’s instructions and styles to every C# file you read or touch.
- Conform to `.editorconfig` and `.codex/style.md` proactively (braces, explicit types, using order, 120-col wraps, StringComparison, etc.).
- Prefer minimal, mechanical, behavior-preserving diffs; do not refactor or change semantics.
- Limit fixes to style/analyzer warnings (IDE*/CA*) that are unambiguous and safe.

## Workspace & Paths
- Prefer `AppWorkspace` for all project paths (client/server/build/dist/shared) instead of hardcoded strings.
- Use `Engine.Extensions` helpers for path/file ops:
  - `PathExtensions`: `Combine`, `DirectoryName`, `Exists`, `Files`, `Folders`, `Create`.
  - `DirectoryExtensions`: `CreateSubDirectory`, `CopyToAsync`, etc.
- Avoid manual string manipulation for paths (e.g., `Replace("src", "build")`). Compute using known roots from `AppWorkspace` and `Path.GetRelativePath` when needed.
- Centralize folder/file/extension literals in `Engine/Constants.cs` (`Folders`, `Files`, `FileExtensions`).
