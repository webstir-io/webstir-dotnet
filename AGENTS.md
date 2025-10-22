# Agent Onboarding

## Quick Start
- Read `.codex/instructions.md` before making any edits.
- Review `.codex/style.md` so your changes match the enforced conventions.
- Check `.codex/testing.md` when planning validation or reporting on test coverage.

## Reference Map
- `.codex/instructions.md` — engineering principles, workflow rules, and workspace utilities.
- `.codex/style.md` — formatting, naming, and C# language preferences used in this repo.
- `.codex/testing.md` — testing philosophy, required suites, and execution commands.

## Daily Reminders
- Keep diffs minimal, behavior-preserving, and focused on the request.
- Respect existing uncommitted changes you did not author.
- Communicate in straightforward, concise language.
- Use repo helpers (`AppWorkspace`, `Engine.Extensions`) for paths and file operations.
- Run terminal commands with `bash -lc`, set `workdir`, and prefer `rg` for searches.
- Use `./utilities/scripts/format-build.sh` before handoff; it now runs project-by-project to avoid the earlier `dotnet format` timeouts.
