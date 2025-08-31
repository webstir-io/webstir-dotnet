# Core Pipeline v2 (Pipelines/Core)

## Goal
Provide simple core primitives and diagnostics under `Engine/Pipelines/Core`, used by CSS/JS/HTML pipelines without introducing a `Shared` layer.

## Files to Create (design)
- `Engine/Pipelines/Core/Diagnostics.cs`
  - `DiagnosticLevel`, `Diagnostic`, `DiagnosticCollection`
  - Methods: `AddError`, `AddWarning`, `AddInfo`, `HasErrors`, summary formatting
- `Engine/Pipelines/Core/BundleResult.cs`
  - `BundleResult` with `Success`, `Files`, `Diagnostics`, `Metrics`
  - `OutputFile` with optional source map content/path
- `Engine/Pipelines/Core/BundlerBase.cs`
  - Base with logger, diagnostics, simple hooks: `BundleAsync`, `PreProcessAsync`, `PostProcessAsync`
  - Utility: `CleanOutputAsync`, `CreateResult(...)`

Note: Names/shape mirror your existing v2 doc but live in `Engine/Pipelines/Core/*` only.

## Design Constraints
- No generics on core interfaces.
- Use enums for module types and simple dependency lists (`List<string>`).
- Thread `DiagnosticCollection` through all pipelines.

## Validation Checklist
- Add files under `Engine/Pipelines/Core/*` only.
- Thread `DiagnosticCollection` through CSS/JS/HTML handlers/bundlers where practical.
- `dotnet build` succeeds; no behavior change yet.

