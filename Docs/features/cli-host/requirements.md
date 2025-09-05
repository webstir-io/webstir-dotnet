# Requirements: CLI Host

- Scope: command UX, help, argument parsing, DI boot, workflow selection, and exit codes.

## Customer Requirements (CR)
- Clear commands with predictable names and flags.
- Built-in help with examples and per-command details.
- Ability to run in any working directory or target a path.
- Clear error messages and stable exit codes.

## Product Requirements (PR)
- Provide commands: `init`, `build`, `publish`, `watch`, `add page`, `add test`, `test`, `help`.
- Global help and per-command help with usage, options, and examples.
- Path parameter handling: last arg can be a path; defaults to current directory.
- Colorized, concise output; errors are easy to spot.

## Software Requirements (SR)
- Map command tokens to workflows; pass through args unmodified.
- Implement `help` command that renders available commands and details.
- Resolve working path: current directory by default; override via last arg path.
- Return non-zero exit code on fatal errors; zero on success.
- Log via configured logger without leaking stack traces by default.

## Acceptance Criteria
- Running without args starts `watch` in current directory.
- `help` shows commands and `help <command>` shows details.
- Providing a path runs the command against that directory.
- Unknown command yields a clear error and help hint.
