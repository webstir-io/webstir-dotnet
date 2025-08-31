# Minimal Tokenizer (Pipelines/Core/Parsing)

## Goal
Implement a minimal tokenizer and parsers that robustly extract dependencies without a full JS/CSS parse. Lives under `Engine/Pipelines/Core/Parsing`.

## Scope
- Tokens: string literals (' " ` with escapes), identifiers, numbers, braces/parens/brackets, commas/semicolons, operators as needed.
- Comments: `//` single-line, `/* ... */` multi-line.
- Special cases: `@import`, `url()`, `import`, `export`, `from`, `as`, `*`.
- Newlines and whitespace for line/column tracking.

## Parsers
- `JavaScriptParser`
  - Extracts: default/named/mixed/namespace imports, side-effect imports, re-exports (`export {...} from`, `export * from`), detect `import(...)`.
  - Outputs: list of import/export statements (file, line, column, names).
  - Ignores: expressions beyond string/identifier boundaries; not an AST.
- `CssImportParser`
  - Extracts: `@import 'x.css'` and `@import url('x.css')` with optional media query tail.

## Error Model
- All tokenization/parsing errors recorded via `DiagnosticCollection` with file/line/column.
- Non-fatal: record error and continue scanning to find additional issues.

## Integration Plan
- JS: Replace only import/export extraction in the graph/build steps; keep existing transformation/minification initially.
- CSS: Replace only `@import` extraction; keep URL rewrite/minification as-is.

## Validation Checklist
- Tokenizer handles embedded comments/strings and unterminated strings gracefully.
- Parsers return accurate line/column for each import/export/@import.
- Real-world test snippets pass (fixtures with quotes, backticks, comments, and whitespace).

