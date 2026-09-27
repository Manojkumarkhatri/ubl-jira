# Deploying U-PMS (Phase 1)

U-PMS is one ASP.NET Core application (`Upms.Web`) and one SQL Server database. The same build runs as a
Linux container or on Windows Server with IIS (research R25). This guide covers both, the configuration keys,
TLS, data protection and the monitoring that supports the 99.5% availability target (SC-009).

## What you need

| Component | Requirement |
|-----------|-------------|
| Database | SQL Server 2022 or later, or Azure SQL Database. One database, full recovery model (see [backup-restore.md](backup-restore.md)). |
| Application | .NET 10 runtime (ASP.NET Core). Linux container, or Windows Server 2019+ with IIS 10 and the ASP.NET Core Hosting Bundle for .NET 10. |
| Network | HTTPS only, with a certificate trusted by company browsers. **WebSockets must be allowed** end to end (Blazor Server keeps one connection per open tab). |
| Sizing (pilot) | 2 vCPU / 4 GB for the app; 4 vCPU / 16 GB for SQL Server. The performance suite (SC-002) passed 300 concurrent users on 500,000 work items on a single 4-vCPU host running both. |

## Configuration

Settings come from environment variables (or IIS `web.config` environment variables, or the company secret
store). Nothing secret is committed or baked into the image. Environment variable names use `__` for `:`.

| Key (environment variable) | Required | Purpose |
|----------------------------|----------|---------|
| `ConnectionStrings__Default` | yes | SQL Server connection string with `Encrypt=True` and a server certificate the app trusts (do not use `TrustServerCertificate=True` outside development). Use a dedicated login with `db_datareader`, `db_datawriter` and `EXECUTE`; migrations need `db_ddladmin` (see "Database migrations"). |
| `Setup__Token` | until setup | Long random string for the first-run page `/setup` (FR-002). Remove it once the first administrator exists; `/setup` then returns 404 anyway. |
| `App__PublicBaseUrl` | yes | The public HTTPS address, for example `https://upms.example.internal`. The Blazor connection is refused from any other origin. |
| `ASPNETCORE_ENVIRONMENT` | yes | `Production`. |
| `ForwardedHeaders__Enabled` | behind a proxy | `true` when a reverse proxy or load balancer terminates TLS and forwards `X-Forwarded-For` and `X-Forwarded-Proto`. The app then trusts these headers from any sender, so **only the proxy may reach the app** (bind to a private network). |
| `DataProtection__CertificatePath`, `DataProtection__CertificatePassword` | recommended | A PFX certificate that encrypts the data protection keys stored in the database (sign-in cookies and anti-forgery tokens depend on them). Keep the same certificate across instances and restores. |
| `Database__MigrateOnStartup` | no (default `false`) | `true` applies pending migrations when the app starts. Prefer a separate migration step in production. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` (and other `OTEL_*`) | no | Sends traces and metrics (ASP.NET Core, SQL client, runtime) to an OpenTelemetry collector. |
| `RateLimiting__SignInPerMinute`, `RateLimiting__SetupPerMinute`, `RateLimiting__KeepAlivePerMinute` | no | Defaults 10, 5 and 6 per minute (contracts/http-endpoints.md). Leave them unless a shared proxy address makes all users look like one client. |
| `Logging__LogLevel__Default` | no | `Information` by default; logs are structured JSON on standard output. |

Organization settings that users see (default time zone, 30-minute idle timeout) live in the database.

## Database migrations

Migrations are part of the build. Before starting a new version:

```bash
dotnet tool restore
dotnet ef migrations script --idempotent --project src/Upms.Infrastructure --startup-project src/Upms.Web -o upms.sql
sqlcmd -S <server> -d <database> -G -i upms.sql     # or run it with your usual change process
```

Review the script like any other change (constitution IV). The append-only triggers on `WorkItemChanges`
and `AuditEvents` are created by the migrations; do not remove them.

## Option A: Linux container

The repository's `Dockerfile` builds a non-root image that listens on port 8080 over HTTP; TLS terminates at
the ingress or reverse proxy.

```bash
docker build -t upms:1.0.0 .
docker run -d --name upms -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__Default="$UPMS_CONNECTION" \
  -e App__PublicBaseUrl=https://upms.example.internal \
  -e ForwardedHeaders__Enabled=true \
  -e DataProtection__CertificatePath=/run/secrets/upms-dp.pfx \
  -e DataProtection__CertificatePassword="$UPMS_DP_PASSWORD" \
  -v /secure/upms-dp.pfx:/run/secrets/upms-dp.pfx:ro \
  upms:1.0.0
```

The proxy must:

- pass WebSocket upgrades (`Upgrade` and `Connection` headers) and allow idle connections of at least
  35 minutes (the app ends idle sessions at 30 minutes itself);
- send `X-Forwarded-For` and `X-Forwarded-Proto`;
- use **sticky sessions** if more than one app instance runs (a Blazor circuit lives in one process).

## Option B: Windows Server with IIS

1. Install the ASP.NET Core Hosting Bundle for .NET 10 and enable the IIS **WebSocket Protocol** feature
   (Server Manager → Web Server → Application Development → WebSocket Protocol).
2. Publish: `dotnet publish src/Upms.Web -c Release -o C:\inetpub\upms`.
3. Create an application pool with **No Managed Code**, identity with read access to the folder and the
   certificate, and "Idle Time-out" at least 35 minutes (or 0).
4. Create the site with an HTTPS binding and the company certificate; remove the HTTP binding or redirect it.
5. Set the configuration keys as environment variables for the site (`web.config` `<environmentVariables>`
   under `aspNetCore`, or IIS Configuration Editor). Do not store the connection string in the folder in
   plain text; use the secret store or a SQL login with Windows authentication (`Integrated Security=true`)
   for the pool identity.
6. With IIS terminating TLS in-process, leave `ForwardedHeaders__Enabled` unset.

## TLS and browser security

- HTTPS only. The app redirects HTTP to HTTPS, sends `Strict-Transport-Security` on HTTPS responses, and marks
  its cookies `Secure`, `HttpOnly` and `SameSite=Lax`.
- A strict Content Security Policy is sent (scripts from the site only). Do not inject third-party scripts.
- Use TLS 1.2 or later on the proxy or IIS.

## First run

1. Apply migrations, start the app with `Setup__Token` set.
2. Open `https://<address>/setup`, enter the token and create the first administrator (FR-002).
3. Remove `Setup__Token` and restart.
4. Sign in as the administrator and add users at `/admin/users`; each receives a temporary password to change at
   first sign-in (FR-003).
5. Make a second person an administrator there ("Make administrator", FR-008), so that accounts can still be
   managed when one administrator is away. The last active administrator can neither lose the role nor be
   deactivated.

## Monitoring and alerting (SC-009)

| Check | Target | Alert |
|-------|--------|-------|
| `GET /health/live` | 200 | the process is down or hung: restart it (container restart policy, IIS rapid-fail protection) |
| `GET /health/ready` | 200 within 2 s | the app cannot reach the database: page the on-call engineer after 2 failed probes, 1 minute apart |
| Error rate (5xx) from the proxy or OpenTelemetry | below 1% over 5 minutes | investigate |
| p95 response time (OpenTelemetry `http.server.request.duration`) | below 1 s | investigate against SC-002 |
| SQL Server backups | every 15 minutes (log) | alert on any missed log backup (see [backup-restore.md](backup-restore.md)) |

Both health endpoints are anonymous and return no internal details. Probe `/health/ready` every minute from
outside the host. 99.5% a month allows about 3.6 hours of downtime, including planned maintenance; announce
maintenance windows and keep them short (a deployment is a restart of seconds).

## Upgrading

1. Back up the database (or confirm the last log backup).
2. Apply the new version's migration script.
3. Deploy the new image or folder. Open tabs reconnect automatically; unsaved typing in an open editor may need
   to be re-entered after a restart.
4. Check `/health/ready`, sign in and open a board.
