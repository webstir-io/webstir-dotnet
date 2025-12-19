# CSS Playbook

This starter uses a small, convention-first CSS system. The goal is to keep styling predictable and reduce "naming entropy" (lots of one-off classes and tokens).

## Principles

- Tokens-first: use CSS custom properties instead of ad-hoc `#hex`/`px` values.
- Tiny surface area: prefer a few layout building blocks + a few components over "utility everything".
- Deterministic cascade: use cascade layers so "where do I put this?" is obvious.
- Accessible defaults: focus rings, readable type, and sensible spacing are part of the system.

## The Contract (v0)

### Layout (classes)

Use a small fixed set of layout building blocks:

- `.ws-container` - centered max-width container
- `.ws-stack` - vertical layout with gap
- `.ws-cluster` - inline row with wrap + gap
- `.ws-grid` - responsive grid
- `.ws-sidebar` - content + sidebar layout

Tune layout building blocks with CSS variables instead of creating more classes:

```html
<div class="ws-stack" style="--ws-gap: var(--ws-space-4)">
  ...
</div>
```

### Components (`data-ui`)

Target components by `data-ui` and use a small set of variant attributes:

- `data-ui="btn"` (and other component ids)
- `data-variant="solid|ghost|outline|soft"`
- `data-size="sm|md|lg"`
- `data-tone="neutral|accent|danger|success|warning"`

Example:

```html
<a data-ui="btn" data-variant="solid" data-tone="accent" href="/about">About</a>
```

Prefer `aria-*` and native attributes for state:

- `aria-current="page"`, `aria-expanded="true|false"`, `disabled`, etc.

### Tokens

Define and customize design values via CSS variables (custom properties).

Common token categories:

- Color: `--ws-bg`, `--ws-fg`, `--ws-muted`, `--ws-border`, `--ws-accent`
- Space: `--ws-space-1..8`
- Radius: `--ws-radius-1..3`
- Type: `--ws-font-sans`, `--ws-font-mono`

### Scoping

Docs-only rules should be scoped under a stable attribute:

- `[data-scope="docs"] { ... }`

This keeps "docs chrome" styles from leaking into app pages.
