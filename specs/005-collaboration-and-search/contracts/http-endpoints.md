# Contract: HTTP Endpoints (Phase 4)

**Feature**: [spec.md](../spec.md) | **Permissions**: [permissions.md](./permissions.md)

The UI keeps running over the Blazor Server circuit. Uploads also travel over the circuit (research R13), so
Phase 4 adds only two read endpoints, for downloading and previewing attachments. Every Phase 1 endpoint and
header (`specs/002-kanban-project-core/contracts/http-endpoints.md`) stays.

| Method & path | Auth | Purpose | Responses |
|---------------|------|---------|-----------|
| `GET /attachments/{id}` | signed in | Download a file | 200 with the file, `Content-Type: application/octet-stream` (images keep their image type), `Content-Disposition: attachment` with the encoded file name; 404 for anything the caller may not have (rule 3 of permissions.md); 401 when not signed in |
| `GET /attachments/{id}/preview` | signed in | Show an image inline (drawer previews, images in formatted text) | 200 with the image and `Content-Disposition: inline`, for PNG, JPEG, GIF and WebP only; 404 for other types and for anything the caller may not have; 401 when not signed in |

- **Checks on every request** (FR-027): the file passed its malware check, is neither removed nor purged, its work
  item is not deleted, and the caller can see the item now (member of its project, or administrator). All
  refusals look the same (404, no body details).
- **Response headers** on both endpoints, besides the Phase 1 headers: `X-Content-Type-Options: nosniff`,
  `Content-Security-Policy: default-src 'none'; sandbox`, `Cache-Control: private, no-store`,
  `Cross-Origin-Resource-Policy: same-origin`. Files are streamed from the database; ranges are not supported.
- **Rate limit**: 120 requests per minute per user across both endpoints (images in long comment threads load
  many previews).
- **Logging**: the attachment id, the result and the correlation id; never the file name or content.
- The page's Content Security Policy is unchanged: `img-src 'self' data:` already allows previews, and the editor
  bundle is served from the site (`script-src 'self'`).
