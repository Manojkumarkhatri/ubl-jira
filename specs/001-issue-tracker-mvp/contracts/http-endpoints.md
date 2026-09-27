# Contract: HTTP Endpoints

**Feature**: [spec.md](../spec.md)

The UI runs over the Blazor Server circuit, so the app exposes only a few plain HTTP endpoints. There
is **no public REST API** in the MVP (spec Assumptions). All endpoints are served over HTTPS only,
with HSTS (research R26).

| Method & path | Auth | Purpose | Responses |
|---------------|------|---------|-----------|
| `GET /Account/Login`, `POST /Account/Login` | anonymous | Sign-in form (static server-rendered, antiforgery token) | 302 to return URL or forced password change; form errors re-rendered with input kept; locked-out message after 5 failures (FR-004) |
| `POST /Account/Logout` | signed in | Sign out (antiforgery) | 302 to `/Account/Login` |
| `GET/POST /Account/ChangePassword` | signed in | Forced change for temporary passwords (FR-003); also reachable from profile | 302 to `/` on success |
| `GET/POST /setup` | anonymous | First-run administrator creation, needs the setup token (FR-002, research R8) | 404 once setup is complete |
| `POST /account/keepalive` | signed in | Renews the sliding authentication cookie when the user chooses "Stay signed in" (FR-005) | 204; 401 if already expired |
| `GET /attachments/{id}` | signed in | Download an attachment (FR-055) | 200 file stream; 404 if no view access, not `Clean`, removed, or purged (existence not revealed) |
| `GET /attachments/{id}/preview` | signed in | Inline image preview (images only) | 200 image; 404 as above or when not an image |
| `GET /health/live` | anonymous | Process liveness | 200 `Healthy` |
| `GET /health/ready` | anonymous | Readiness for the load balancer | 200 `Healthy` or `Degraded` (scanner/SMTP down), 503 `Unhealthy` (database down); no internal details in the body |
| `/_blazor` (framework hub) | signed in | Interactive Server circuit | cross-origin WebSocket requests rejected (Origin must match `App:PublicBaseUrl`) |

## Response headers

- All responses: `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`,
  `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, and a
  `Content-Security-Policy` with `default-src 'self'` (research R26).
- Attachment downloads: `Content-Disposition: attachment; filename*=UTF-8''…`, the content type
  verified at upload, `Content-Security-Policy: sandbox`, and `Cache-Control: private, no-store`.
  Previews use `inline` and are served only for PNG, JPEG, GIF, and WebP.

## Rate limits (ASP.NET Core rate limiting)

- `POST /Account/Login`: 10 requests per minute per client IP (in addition to account lockout).
- `POST /setup`: 5 requests per minute per client IP.
- `POST /account/keepalive`: 6 requests per minute per user.
