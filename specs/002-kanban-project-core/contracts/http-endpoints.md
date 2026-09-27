# Contract: HTTP Endpoints (Phase 1)

**Feature**: [spec.md](../spec.md)

The UI runs over the Blazor Server circuit; Phase 1 exposes only these plain HTTP endpoints and no
public API. All are HTTPS-only with HSTS (research R9).

| Method & path | Auth | Purpose | Responses |
|---------------|------|---------|-----------|
| `GET/POST /Account/Login` | anonymous | Sign-in form (static server-rendered, anti-forgery) | 302 to the return URL or to the forced password change; errors re-render with input kept; lockout message after 5 failures |
| `POST /Account/Logout` | signed in | Sign out (anti-forgery) | 302 to `/Account/Login` |
| `GET/POST /Account/ChangePassword` | signed in | Forced change after a temporary password; voluntary change | 302 to `/projects` |
| `GET/POST /setup` | anonymous | First-run administrator creation with the setup token | 404 once setup is complete |
| `POST /account/keepalive` | signed in | Renews the sliding cookie when the user chooses "Stay signed in" | 204; 401 if already expired |
| `GET /health/live` | anonymous | Process liveness | 200 `Healthy` |
| `GET /health/ready` | anonymous | Readiness (database) | 200 `Healthy` or 503 `Unhealthy`; no internal details |
| `/_blazor` | signed in | Interactive Server circuit | cross-origin WebSocket requests rejected |

- Response headers on every response: `Strict-Transport-Security`, `Content-Security-Policy`
  (`default-src 'self'`), `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: strict-origin-when-cross-origin`.
- Rate limits: `POST /Account/Login` 10 per minute per client IP; `POST /setup` 5 per minute per IP;
  `POST /account/keepalive` 6 per minute per user.
