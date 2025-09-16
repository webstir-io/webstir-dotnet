# Engineering Instructions

## Core Principles
- Keep the solution simple (KISS) and apply YAGNI so work stays easy to evolve.
- Follow DRY and SOLID; duplication and tight coupling make future changes harder.
- Favor elegant code: readable, consistent, concise, and debuggable.
- Let the code speak for itself—avoid unnecessary comments.
- Communicate with straightforward, concise language.

## Workflow Expectations
- Apply these instructions and `.codex/style.md` to every C# file you touch.
- Prefer minimal, mechanical, behavior-preserving diffs; don’t refactor beyond the ask.
- Fix only clear IDE*/CA* style warnings that are safe and unambiguous.
- Respect existing uncommitted changes that you did not create.
- Follow `.editorconfig` and repository conventions proactively.

## Workspace Practices
- Use `AppWorkspace` when resolving project paths instead of hardcoded strings.
- Rely on `Engine.Extensions` helpers for file and directory operations (`PathExtensions`, `DirectoryExtensions`).
- Avoid manual string manipulation for paths; use `Path.GetRelativePath` when needed.
- Centralize folder, file, and extension literals in `Engine/Constants.cs` (`Folders`, `Files`, `FileExtensions`).

## Editing & Tooling
- Default to ASCII when editing or creating files unless the file already uses non-ASCII characters.
- Add comments only when they provide essential context that code cannot.
- Run shell commands with `bash -lc`, always setting the working directory explicitly.
- Prefer `rg`/`rg --files` for searches; fall back to other tools only if unavailable.
- Use `./utilities/format-build.sh` before handing off work or when format/build checks fail.

## Testing
- Consult `.codex/testing.md` for the testing philosophy, required coverage, and command references.
