# Minimal Tokenizer (Pipelines/Core/Parsing)

## Goal
Implement a minimal tokenizer and parsers that robustly extract dependencies without a full JS/CSS parse. Lives under `Engine/Pipelines/Core/Parsing`.

## Non-Goals (v1)
- Full JavaScript or CSS ASTs.
- JSX/TSX, decorators, or advanced syntax transforms.
- CommonJS interop.
- CSS parsing beyond `@import` and `url()` extraction.

## Token Model
- Token kinds: `Identifier`, `StringSingle`, `StringDouble`, `TemplateHead`, `TemplateMiddle`, `TemplateTail`, `Number`, `LParen`, `RParen`, `LBrace`, `RBrace`, `LBracket`, `RBracket`, `Comma`, `Semicolon`, `Dot`, `Star`, `Slash`, `At`, `Colon`, `Question`, `Less`, `Greater`, `Equal`, `Plus`, `Minus`, `Backtick`, `Whitespace`, `Newline`, `CommentLine`, `CommentBlock`, `Unknown`.
- Position data: absolute `Start`/`End` offsets, `Line`, `Column` for each token; file path provided by caller.
- Modes: base, string (single/double), template literal with interpolation tracking (`${ ... }`). Do not parse inside JS expressions for content—only track boundaries to recover and continue.
- Minimal allocation: store `Start`/`End` spans; slice only when emitting final values.

## Whitespace & Comments
- Skip emitting `Whitespace` by default; maintain line/column tracking.
- Support `// ...\n` and `/* ... */`; recover on EOF with a diagnostic for unterminated block comment.

## Keywords (for JS parser convenience)
- `import`, `export`, `from`, `as`, `default`, `type`, `assert`.
- Parser compares `Identifier` token value against this set (case-sensitive).

## Parsers
- JavaScriptParser
  - Extracts: default/named/mixed/namespace imports; side-effect imports; re-exports (`export { ... } from`, `export * from`, `export * as ns from`); detect `import(...)` calls.
  - Ignores: any expression details beyond recognizing a string literal immediate argument to `import(...)`. No AST.
  - Ignores TypeScript type-only imports: `import type { X } from 'mod'` does not create a runtime dependency.
  - Recognized forms:
    - `import Default from 'mod'`.
    - `import { A, B as C } from 'mod'`.
    - `import * as NS from 'mod'`.
    - `import Default, { A, B as C } from 'mod'`.
    - `import 'mod'` (side effect).
    - `export { A, B as C } from 'mod'`.
    - `export * from 'mod'`.
    - `export * as NS from 'mod'` (namespaced re-export).
    - `import('mod')` (dynamic import with string literal immediate arg).
  - Not recognized (v1): `import.meta`, `assert { type: 'json' }` tail is tolerated and skipped if present, not parsed.

- CssImportParser
  - Extracts `@import` followed by either a string or `url(...)` with optional quotes.
  - Captures optional trailing media query list until the terminating semicolon.
  - Recognized forms:
    - `@import 'x.css';`
    - `@import "x.css" screen and (min-width: 600px);`
    - `@import url('x.css') print;`
    - `@import url(x.css);`
  - Comments and whitespace anywhere are ignored. Nested `url(...)` are not supported.

## Output Types
- `ParsedImport`
  - Fields: `Source` (string), `Kind` (`Static`, `ReExport`, `Dynamic`), `Names` (list of `ImportedName { Local, Imported, IsNamespace, IsDefault }`), `StartLine`, `StartColumn`.
- `ParsedCssImport`
  - Fields: `Href` (string), `Media` (string? null when absent), `StartLine`, `StartColumn`.

## Error Model
- Record all errors via `DiagnosticCollection` (file, line, column, message). No exceptions unless truly unrecoverable I/O.
- Non-fatal: continue scanning to report multiple issues.
- Typical diagnostics:
  - Unterminated string literal.
  - Unterminated block comment.
  - `@import` without terminating semicolon.
  - `import`/`export` without a following string literal where required.

## Recovery Strategy
- Strings: on EOF without closing quote/backtick, emit diagnostic and treat until EOF as a single string token.
- Template literals: track `${` depth; if EOF before closing `` ` ``, emit diagnostic and resume.
- JS parser: upon mismatch, skip to next `Semicolon` or next `import`/`export` keyword token.
- CSS parser: for bad `@import`, skip to next semicolon.

## API Surface (Engine/Pipelines/Core/Parsing)
- Files:
  - `Token.cs` — `enum TokenKind`, `readonly struct Token` with position/span.
  - `Tokenizer.cs` — `Tokenizer` with `bool TryReadNext(out Token token)` and `Reset(ReadOnlySpan<char> text)`.
  - `JavaScriptParser.cs` — `IEnumerable<ParsedImport> ParseImports(...)` and `IEnumerable<ParsedImport> ParseReExports(...)` over a token stream.
  - `CssImportParser.cs` — `IEnumerable<ParsedCssImport> ParseImports(...)` over a token stream.
  - `ParsedImport.cs`, `ParsedCssImport.cs` — simple DTOs.
- Design:
  - Explicit types; no `var` except anonymous types.
  - Pure, side-effect-free methods wherever possible.
  - All methods accept `DiagnosticCollection diagnostics` and append findings.

## Integration Plan
- JavaScript pipeline:
  - Replace only dependency extraction for graphing with `JavaScriptParser` results.
  - Keep current transformation/minification pipeline.
- CSS pipeline:
  - Use `CssImportParser` to resolve and order `@import`s.
  - Preserve existing URL rewriting, CSS Modules behavior, minification.

## Examples
- JavaScript
  - `import foo from './foo.js'` → `ParsedImport { Source: './foo.js', Kind: Static, Names: [ Default ] }`.
  - `import { a, b as c } from '../lib.mjs'` → `Names: [ {Imported:a, Local:a}, {Imported:b, Local:c} ]`.
  - `import * as ns from 'pkg'` → `Names: [ {IsNamespace:true, Local:ns} ]`.
  - `export * from './x.js'` → `Kind: ReExport`.
  - `import('dyn.js')` → `Kind: Dynamic` with `Source: 'dyn.js'`.
  - `import(meta)` or `import(
      someVar
    )` → not a static dependency; no `Source`, optionally emit informational diagnostic if helpful.
- CSS
  - `@import url("/styles/base.css") screen and (min-width: 600px);` → `Href: '/styles/base.css'`, `Media: 'screen and (min-width: 600px)'`.

## Edge Cases & Rules
- Ignore `import type { X } from '...';` for runtime graph.
- Allow trailing commas in import specifiers.
- Allow comments between tokens (e.g., `import/*x*/{A}/*y*/from/*z*/'m'`).
- CSS: allow whitespace/comments after `@import` and inside `url(...)`.

## Performance Notes
- Single pass tokenizer; parsers are streaming and backtrack minimally.
- Avoid substring allocations until emitting `Source/Href`.
- Line/column tracking maintained incrementally.

## Validation Checklist
- Tokenizer handles comments, strings (including templates) and unterminated constructs gracefully.
- JavaScript parser extracts all listed import/export forms with accurate line/column.
- `import type` is ignored; `import(...)` with string literal is detected as dynamic.
- CSS parser supports string and `url(...)` forms; media tail captured accurately.
- Comments/whitespace do not break detection in both JS and CSS.
