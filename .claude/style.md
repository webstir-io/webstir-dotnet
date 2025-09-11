# C# Style Essentials

- Use file-scoped namespaces (`namespace Foo.Bar;`)
- Use Allman style braces consistently
- Indent with 4 spaces, no tabs
- Use PascalCase for types, methods, and properties
- Use camelCase for local variables and parameters
- Prefix interfaces with `I`
- Use ALL_CAPS for `const` fields
- Always use braces, even for single-line statements
- Prefer `var` when the type is obvious; otherwise use explicit types
- Async method names should end with `Async`
- Follow DRY, KISS, and YAGNI principles

# C# Standard Style (Microsoft / Roslyn)

- **Namespaces & Usings**
  - Use **file-scoped namespaces**.
  - Place `using` directives **outside namespaces**.
  - Sort alphabetically, `System.*` first.
  - Remove unused `using`s.

- **Braces & Indentation**
  - Use **Allman braces** (opening `{` on a new line).
  - **Always use braces**, even for single-line statements.
  - Indent with **4 spaces** (no tabs).

- **Naming**
  - **PascalCase** for types, methods, properties, events.
  - **camelCase** for locals and parameters.
  - Prefix interfaces with **I**.
  - Constants in **ALL_CAPS**.
  - Async methods end with **Async**.
  - Use descriptive, non-abbreviated names.
  - Do not use single-letter names anywhere (variables, parameters, fields, properties, methods, types). No exceptions.
  - Use the discard identifier `_` for intentionally unused parameters or lambda arguments.
  - Prefer domain terms and full words (e.g., `token` not `t`, `buffer` not `buf`).

- **Types & Members**
  - Prefer **records** for immutable data.
  - Mark classes **sealed** unless designed for inheritance.
  - Use **readonly** where possible.
  - Use `init` or `required` for immutability/invariants.

- **Expressions & Syntax**
  - Prefer **expression-bodied members** for simple cases.
  - Use **pattern matching** and **switch expressions**.
  - Use `nameof`, discards (`_`), numeric separators.
  - Use **target-typed `new`** where obvious.
  - Use **interpolated strings** instead of concatenation.

- **`var` & Typing**
  - Use `var` for built-ins and when the type is obvious.
  - Use explicit types when the type isn’t obvious.

- **Nullability**
  - Enable **nullable reference types**.
  - Use `??`, `?.`, and guard clauses.
  - Avoid null-forgiving `!` unless unavoidable.

- **Async**
  - Use **async/await** end-to-end.
  - Don’t block on tasks (`.Result`, `.Wait()`).
  - Accept **`CancellationToken`** in async APIs.
  - Use `ConfigureAwait(false)` in libraries.

- **Collections & LINQ**
  - Prefer collection **initializers** and **collection expressions**.
  - Use LINQ for readability (loops in perf hot paths).
  - Avoid unnecessary `ToList()`/`ToArray()`.

- **Error Handling**
  - Throw exceptions only for exceptional cases.
  - Use specific exception types.
  - Never swallow exceptions silently.

- **Comments & Docs**
  - Use XML `///` docs for public APIs.
  - Keep comments concise and meaningful.
  - Avoid redundant comments.

- **Formatting**
  - One space after keywords; spaces around binary operators.
  - No trailing whitespace.
  - Wrap lines at ~120 chars.
  - Long boolean expressions: break onto multiple lines with the operator at the start of the continued line; one condition per line; indent by 4 spaces.
    Example:
    
        return conditionA
            || conditionB
            || conditionC;
    
    Use parentheses when precedence could be ambiguous, and keep `StringComparison` (or other arguments) with the call it modifies.
  - Use blank lines to group related code.

- **General Principles**
  - Follow **KISS, DRY, YAGNI, SOLID**.
  - Favor immutability; use guard clauses; return early.
  - Keep DTOs separate from domain models.
  - Methods & SRP: Prefer ~5–40 lines as a guideline, not a rule. Let Single Responsibility drive size; split when a method does more than one thing (e.g., compute vs. write, parse vs. transform). Use small private helpers; avoid trivial pass‑through wrappers that harm clarity.

## Naming: Single-letter Names
- Prohibited. Always use descriptive names; use `_` only as a discard.

## Literals: Magic Strings
- Avoid magic strings and numbers in code.
- Centralize file names, extensions, and folder names in `Engine/Constants.cs` (`Files`, `FileExtensions`, `Folders`).
- Prefer domain enums or static readonly fields when a constant doesn’t belong in `Constants.cs`.
- Exceptions: user-facing error/help text, narrowly scoped test data, or tiny regex fragments where a constant harms clarity.
