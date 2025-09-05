# Requirements: Auth & Sessions

- Scope: cookie/session primitives, secure defaults, and route protection helpers.

## Customer Requirements (CR)
- Implement login/logout with secure cookies and minimal setup.
- Protect routes and actions based on session/user state.
- Work locally and in production without provider lock‑in.

## Product Requirements (PR)
- Session store interface with a default implementation; pluggable backends.
- Helpers to set/read secure cookies; rotate and invalidate sessions.
- Route guards/middleware to enforce authentication/authorization.
- CSRF helpers and recommendations for forms/actions.

## Software Requirements (SR)
- Sign and optionally encrypt session cookies; set `HttpOnly`, `Secure`, `SameSite` as appropriate.
- Expose `getSession`, `commitSession`, `destroySession` helpers.
- Provide middleware to inject user/session into request context.
- Document provider integrations without coupling to any.

## Acceptance Criteria
- A sample login flow sets a session and redirects to a protected page.
- Accessing a protected route without a session redirects to login.
- Logging out destroys the session; cookies clear in the browser.
