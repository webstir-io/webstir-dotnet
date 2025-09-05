# Requirements: Client Navigation

- Scope: opt‑in SPA‑style links, prefetch, and transitions on top of MPA.

## Customer Requirements (CR)
- Keep MPA as default; enable client navigation when desired.
- Small runtime with accessible links and predictable scroll behavior.
- Prefetch to speed up likely navigations.

## Product Requirements (PR)
- `Link` component and `navigate()` API that use the routing manifest.
- History, scroll restoration, and transition hooks.
- Optional prefetch on hover/viewport with simple heuristics.

## Software Requirements (SR)
- Read the route manifest to resolve URLs and fetch page data/assets.
- Intercept navigations; fallback to full reload on unsupported cases.
- Provide minimal state: current route, params, pending state.

## Acceptance Criteria
- Clicking a `Link` transitions without full reload; back/forward restore scroll.
- Enabling prefetch reduces navigation latency for prefetched routes.
- Disabling client navigation returns to full MPA behavior.
