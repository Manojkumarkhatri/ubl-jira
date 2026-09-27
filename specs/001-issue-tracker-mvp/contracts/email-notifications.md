# Contract: Notification Emails

**Feature**: [spec.md](../spec.md) | Implements FR-057 and FR-058 (research R18)

Emails are sent only for **assignments** and **mentions**, only to active users with notification
emails switched on, and never for the user's own actions. They carry the minimum needed to act and
**never** include the issue description or comment text.

## Common properties

- **From**: `Email:From` setting (for example `U-PMS <upms-noreply@company.example>`).
- **Headers**: `Auto-Submitted: auto-generated`, `X-Auto-Response-Suppress: All`, unique
  `Message-ID`.
- **Formats**: plain text plus a simple, accessible HTML alternative (single column, real text, no
  remote images).
- **Link**: `{App:PublicBaseUrl}/browse/{IssueKey}`; footer link to
  `{App:PublicBaseUrl}/account/profile` to switch emails off.
- **Delivery**: queued in the outbox within the triggering transaction; retried with exponential
  backoff for at least 24 hours; then marked `Expired` (FR-058).

## Assigned

- **Subject**: `[{IssueKey}] {Summary} — assigned to you by {ActorDisplayName}`
- **Body (text)**:

  ```text
  {ActorDisplayName} assigned {IssueKey} to you.

  {IssueKey}: {Summary}
  Project: {ProjectName} · Type: {Type} · Priority: {Priority} · Status: {Status}

  Open the issue: {Link}

  You receive this email because you have notification emails switched on.
  Change this setting: {ProfileLink}
  ```

## Mentioned

- **Subject**: `[{IssueKey}] {Summary} — {ActorDisplayName} mentioned you`
- **Body (text)**:

  ```text
  {ActorDisplayName} mentioned you in {a comment on | the description of} {IssueKey}.

  {IssueKey}: {Summary}
  Project: {ProjectName}

  Open the issue: {Link}

  You receive this email because you have notification emails switched on.
  Change this setting: {ProfileLink}
  ```

## Rules

- Summaries are truncated to 120 characters in the subject; header injection is prevented by
  stripping CR/LF from all interpolated values.
- If the recipient loses access to the project before delivery, the message is dropped.
