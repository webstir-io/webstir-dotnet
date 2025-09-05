# Requirements: Workspace & Project Model

- Scope: workspace initialization, well-known roots, constants, and path helpers.

## Customer Requirements (CR)
- Projects work without manual path configuration.
- Consistent, predictable folder layout and names.
- No magic strings for paths in code.

## Product Requirements (PR)
- Initialize workspace from a working directory.
- Provide roots for client, server, shared, build, and dist.
- Centralize folder/file/extension literals.
- Offer path helper methods to combine, list, and create folders/files.

## Software Requirements (SR)
- `AppWorkspace.Initialize(path)` sets roots and validates structure.
- `Engine/Constants.cs` holds folder/file/extension constants.
- `Engine.Extensions.PathExtensions` exposes safe operations for paths/files.
- Avoid manual string manipulation for paths; compute from known roots.

## Acceptance Criteria
- Consumers can obtain absolute paths to all roots after initialize.
- Helper methods work across platforms and normalize separators.
- No hardcoded relative paths remain where roots are available.

