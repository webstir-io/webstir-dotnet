# C# Style Guide

## Layout & Structure
- Use file-scoped namespaces with `using` directives outside the namespace block.
- Order `using` directives alphabetically with `System.*` first and remove unused entries.
- Apply Allman braces everywhere and include braces even for single-line statements.
- Indent with 4 spaces, never tabs, and keep lines to ~120 characters.
- Group related code with blank lines and break long boolean expressions one condition per line.

## Naming
- PascalCase types, methods, properties, and events.
- camelCase locals and parameters; prefix interfaces with `I`.
- Use ALL_CAPS for constants and give everything descriptive, non-abbreviated names.
- Do not use single-letter identifiers; prefer `_` only for discards.

## Language Preferences
- Use `var` when the type is obvious; otherwise spell out the type.
- Favor expression-bodied members for simple implementations.
- Prefer guard clauses and early returns to reduce nesting.
- Use interpolated strings, pattern matching, switch expressions, and target-typed `new` where they improve clarity.
- Always select overloads that accept `StringComparison` when comparing strings.
- Embrace nullable reference types and avoid the null-forgiving operator unless unavoidable.

## Types & Immutability
- Prefer records for immutable data transfer scenarios.
- Mark classes `sealed` unless they are meant for inheritance.
- Use `readonly`, `init`, or `required` members to encode invariants.

## Async & Concurrency
- Async method names end with `Async` and accept `CancellationToken` when appropriate.
- Use async/await end-to-end; do not block on tasks.
- Call `ConfigureAwait(false)` inside library code when awaiting tasks.

## Collections & LINQ
- Use collection initializers or collection expressions when they keep code clear.
- Prefer LINQ for readability but avoid unnecessary `ToList()`/`ToArray()` calls.

## Error Handling
- Throw exceptions only for exceptional cases and pick the most specific type available.
- Never swallow exceptions silently; surface meaningful context instead.

## Documentation
- Use XML `///` comments for public APIs and keep any other comments concise and purposeful.

## File Sizes
- Please try to keep file size to around 500 lines or less if possible.