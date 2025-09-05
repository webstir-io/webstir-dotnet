# Requirements: Forms & Actions

- Scope: server actions with validation, CSRF, and progressive enhancement.

## Customer Requirements (CR)
- Handle form submissions without client JS; enhance when JS is available.
- Validate inputs and surface clear field errors.
- CSRF protection and session‑aware actions.

## Product Requirements (PR)
- Convention: `pages/<route>/action.server.ts` exporting HTTP method handlers (`POST`, etc.).
- Helpers: `parseForm()`, `validate(schema)`, `csrf()`.
- Result shapes: success with redirect or data; failure with field/global errors.

## Software Requirements (SR)
- Route actions under a predictable URL; support method dispatch and 405 for missing handlers.
- Parse `application/x-www-form-urlencoded` and `multipart/form-data`.
- CSRF token generation/verification integrated with sessions.
- Standardize error mapping to forms for SSR and enhanced submissions.

## Acceptance Criteria
- A non‑JS form submission invokes the server action and returns a new page.
- With JS, the same form enhances to fetch and updates the page without full reload.
- Invalid input shows field errors; CSRF mismatch returns a clear error and no state change.
