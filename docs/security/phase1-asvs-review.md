# Phase 1 security review against OWASP ASVS 4.0.3, Level 2

**Scope**: U-PMS Phase 1 (sign-in, accounts, projects, board, task drawer, column settings) as built on branch
`claude/practical-lamport-l47ei8`, 2026-09-27. **Method**: requirement-by-requirement review of the code,
configuration and tests, with a fix and a test for each finding that could be fixed in Phase 1 (constitution
III). Evidence is the automated tests named below; they run in CI.

**Result**: 13 findings fixed during the review; 4 items remain open. Two of the open items are Level 2
requirements that need a decision before rollout beyond the pilot (see [Open items](#open-items)).

## Findings fixed during the review

| # | ASVS | Finding | Fix | Test |
|---|------|---------|-----|------|
| F1 | 2.1.7 | Common passwords from breach data were accepted if at least 12 characters long. | `CommonPasswordValidator` refuses the 1,259 NCSC-listed passwords of 12+ characters, one repeated character, straight runs ("123456789012") and passwords containing the person's name. | `AccountServiceTests.Common_and_easy_to_guess_passwords_are_refused` |
| F2 | 2.1.2 | Passwords had no upper limit (a very long password also costs hashing CPU at sign-in). | 128-character limit when setting a password; sign-in refuses longer input before hashing. | `AccountServiceTests.Passwords_of_up_to_128_characters_are_accepted_and_longer_ones_refused` |
| F3 | 2.1.8 | No password strength feedback. | Strength hint on every new-password field. | `FoundationJourneyTests.A_temporary_password_is_replaced_at_first_sign_in_then_the_profile_opens` |
| F4 | 2.1.12 | Typed passwords could not be checked. | "Show password" toggle (`aria-pressed`) on every password field. | same journey test |
| F5 | 3.3.1 | After signing out, a copied session cookie was still accepted by the host until it expired (the services already refused it). | Every request checks the session's server-side state; signed-out and idle sessions are refused. | `HostSecurityTests.A_copy_of_the_session_cookie_stops_working_after_signing_out`, `SessionPolicyTests` |
| F6 | 3.3.2 | An active session could last indefinitely (30-minute idle timeout only). | Sessions end 12 hours after sign-in (`upms:auth_time` claim, kept across security-stamp refreshes). | `SessionPolicyTests.A_session_ends_twelve_hours_after_signing_in_even_while_in_use` |
| F7 | 3.4.4 | The session cookie had no `__Host-` prefix. | Cookie renamed `__Host-upms.auth` (Secure, HttpOnly, SameSite=Lax, Path=/, no Domain). | `HostSecurityTests.The_session_cookie_is_host_only_secure_http_only_and_same_site` |
| F8 | 7.2.2 | Access-control refusals were not logged. | `ProjectAccess` logs each refusal (event 4030: user ID, right, project key; no content). | covered by `PermissionMatrixTests` (refusals) |
| F9 | 8.2.1 | Pages carried no anti-caching header. | `Cache-Control: no-store` on every response that does not set its own (static files keep theirs). | `HostSecurityTests.Pages_are_not_cached_and_responses_name_no_server_software` |
| F10 | 14.3.3 | Responses named the server software (`Server: Kestrel`). | Header removed. | same test |
| F11 | 1.4.1, 4.1.1 | Found while building the board: overlapping policy checks in one Blazor circuit shared a database context, which crashed the circuit (a denial of service for that user). | Each check reads the user's status in its own scope. | `HostSecurityTests.Policy_checks_that_overlap_in_one_circuit_each_read_the_users_status_safely` |
| F12 | 11.1.4, 2.2.1 | Found by the end-to-end suite: sign-in rate limits were fixed in code. | Limits are configuration with the contract's defaults. | `HostSecurityTests.Sign_in_attempts_are_limited_to_10_per_minute_per_client` |
| F13 | 9.2.2 | The deployment guide did not require an encrypted database connection. | Guide requires `Encrypt=True` with a trusted server certificate. | [deployment.md](../operations/deployment.md) |

## Requirement areas

Status: ✅ met · 🔧 met after a fix above · ⚪ not applicable in Phase 1 · ⚠️ open (see below).

| Area | Status | Evidence |
|------|--------|----------|
| **V1 Architecture** | ✅ | Threat-relevant design in plan.md and research.md (R6–R10); one authorization point `IProjectAccess`; module boundaries enforced by `ModuleBoundaryTests`; secrets only from configuration (Dockerfile, deployment guide). |
| **V2.1 Passwords** | 🔧 | 12–128 characters (F2), no composition rules or rotation, breached and easy passwords refused (F1), strength hint and show toggle (F3, F4), paste and password managers allowed (`autocomplete` attributes), change requires the current password (`AccountService.ChangePasswordAsync`). |
| **V2.2 General authenticator** | ✅ / ⚠️ | Anti-automation: 10 sign-ins per minute per IP and lockout after 5 failures for 15 minutes (`SignInTests`, `HostSecurityTests`). Notification of credential changes (2.2.3, 2.5.x) is open (O3). |
| **V2.3 Authenticator lifecycle** | ✅ / ⚠️ | Temporary passwords are 16 random characters from `RandomNumberGenerator`, shown once, and must be replaced at first sign-in (`UserAdminServiceTests`). They do not expire (2.3.1, a "should"): open (O4). |
| **V2.4 Credential storage** | ✅ | ASP.NET Core Identity v3 hashes: PBKDF2-HMAC-SHA512, 100,000 iterations, per-user salt. |
| **V2.5 Recovery** | ✅ | No self-service recovery or security questions; an administrator issues a new temporary password (audited). |
| **V2.7–2.9 Multi-factor** | ⚠️ | Not in Phase 1: open (O1). |
| **V3 Sessions** | 🔧 / ⚠️ | Data-protected cookie ticket, new session ID per sign-in, 30-minute idle timeout with warning (`IdleSessionWarningTests`), 12-hour absolute limit (F6), server-side end of session on sign-out and idle (F5), `__Host-` cookie (F7), other sessions end within a minute after a password change or deactivation (security stamp checked every minute). Viewing and ending one's other sessions (3.3.4) is open (O2). |
| **V4 Access control** | ✅ / ⚠️ | Server-side checks on every call, default deny, rights read from the database on every call; every cell of the permission matrix tested (`PermissionMatrixTests`, 170 cells), anonymous access redirected (`HostSecurityTests`). Administrator role changes are audited (`RoleChanged`), and the last active administrator can neither lose the role nor be deactivated, even when two administrators act at once (4.1.3). CSRF: anti-forgery tokens on forms and the Blazor origin check. Phase 1 is an open workspace by design (plan.md, Complexity Tracking). Multi-factor for administrators (4.3.1) is open (O1). |
| **V5 Validation and encoding** | ✅ | Domain validation of every field (lengths, formats); plain text only, rendered as text; links only for `http`/`https` with `rel="noopener noreferrer"` (`PlainTextTests`); Razor encodes output; parameterized SQL everywhere (EF Core, interpolated `SqlQuery`); local-only redirects (`AccountEndpoints.IsSafeRelative`, `IdentityRedirectManager`); no untrusted deserialization. |
| **V6 Cryptography** | ✅ | Data protection keys in the database, protected by a certificate in production (deployment guide); fixed-time comparison of the setup token (`SetupService`); cryptographic randomness for temporary passwords. |
| **V7 Errors and logging** | 🔧 | Structured JSON logs without passwords or tokens; sign-in successes, failures, lockouts and account changes in the append-only `AuditEvents` table (update and delete refused by trigger); access refusals logged (F8); generic error page in production. |
| **V8 Data protection** | 🔧 | No sensitive data in URLs (task keys only) or browser storage; `no-store` on pages (F9); temporary passwords shown once and never logged (`AccountServiceTests`). |
| **V9 Communication** | 🔧 | HTTPS only with HSTS (`HostSecurityTests`), TLS 1.2+ at the proxy or IIS, encrypted database connection required (F13). |
| **V10 Malicious code** | ✅ | No third-party scripts (CSP `script-src 'self'`); NuGet audit fails the build on high and critical advisories; dependency versions pinned centrally. |
| **V11 Business logic** | ✅ | Optimistic concurrency on every edit and column change (SC-005 tests); limits on columns (10), text lengths and page sizes (100). |
| **V12 Files** | ⚪ | No uploads or downloads in Phase 1. |
| **V13 API** | ✅ | Only sign-out (anti-forgery), keep-alive (custom header, per-user rate limit) and health endpoints; no CORS. |
| **V14 Configuration** | 🔧 | Warnings as errors and analyzers in CI; debug features only in Development; security headers (CSP, `nosniff`, `X-Frame-Options`, `frame-ancestors`, Referrer-Policy, Permissions-Policy) on every response; no server header (F10). CSP allows inline styles (`style-src 'unsafe-inline'`) for Blazor's style attributes: low risk, accepted (O5 note). |

## Open items

| # | ASVS | Item | Risk | Recommendation |
|---|------|------|------|----------------|
| **O1** | 2.7–2.8, **4.3.1 (L2)** | No multi-factor authentication, including for administrators. | Medium: a phished or reused administrator password gives full access. Compensated during the pilot by internal-network access only, 12+ character non-breached passwords, lockout and the audit log. | **Decision needed before rollout beyond the pilot**: add authenticator-app (TOTP) sign-in for Administrators (ASP.NET Core Identity supports it), or rely on the company single sign-on with MFA. |
| **O2** | **3.3.4 (L2)** | Users cannot list or end their other sessions. | Low–medium: a forgotten session on another computer stays usable until the idle timeout (30 minutes) or the 12-hour limit. Administrators can end all of a user's sessions by resetting the password or deactivating the account. | Add a "Signed-in sessions" list with "Sign out everywhere" to the profile page (Phase 2). |
| **O3** | 2.2.3, 2.5.5 | No notification when a password is changed or reset. | Low: Phase 1 has no e-mail (scope decision); changes are in the audit log. | Send notifications when e-mail arrives (Phase 2). |
| **O4** | 2.3.1 | Temporary passwords do not expire. | Low: they are random, shown once and must be replaced at first sign-in. | Expire unused temporary passwords after 7 days (needs one column). |
| O5 (note) | 14.4.3 | CSP `style-src` allows inline styles. | Low: scripts are restricted to the site; no user HTML is rendered. | Revisit if Blazor removes its inline style needs. |

## Re-running the checks

```bash
dotnet test --project tests/Upms.Application.Tests -- --filter-class "*PermissionMatrixTests" --filter-class "*AccountServiceTests"
dotnet test --project tests/Upms.Web.Tests -- --filter-class "*HostSecurityTests" --filter-class "*SessionPolicyTests"
dotnet test --project tests/Upms.E2E.Tests -- --filter-class "*FoundationJourneyTests"
```
