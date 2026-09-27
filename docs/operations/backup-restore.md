# Backups and restore (Phase 1)

Targets: lose no more than **1 hour** of saved work (RPO) and restore service within **4 hours** (RTO) after
any failure (SC-010), shown by a restore drill before go-live. All U-PMS data, including data protection keys
and the append-only history, is in one SQL Server database, so backing up that database backs up everything.
The data protection certificate (see [deployment.md](deployment.md)) is backed up separately, with the
company's certificate process.

## Backup schedule

| Backup | When | Keep | Purpose |
|--------|------|------|---------|
| Full | daily, 01:00 | 35 days | starting point of every restore |
| Differential | every 6 hours (07:00, 13:00, 19:00) | 7 days | shortens restores |
| Transaction log | every 15 minutes | 7 days | point-in-time restore; RPO of 15 minutes in practice |

The database uses the **full recovery model**. Backups go to storage outside the database server (a backup
share or blob storage) and are checksummed and compressed. Azure SQL Database provides equivalent automatic
backups with point-in-time restore; set its retention to at least 7 days and follow the same drill.

```sql
ALTER DATABASE [Upms] SET RECOVERY FULL;

-- Daily full
BACKUP DATABASE [Upms] TO DISK = N'\\backup\upms\Upms_full_20260927.bak'
  WITH COMPRESSION, CHECKSUM, INIT, STATS = 10;

-- Every 6 hours
BACKUP DATABASE [Upms] TO DISK = N'\\backup\upms\Upms_diff_20260927_1300.bak'
  WITH DIFFERENTIAL, COMPRESSION, CHECKSUM, INIT;

-- Every 15 minutes
BACKUP LOG [Upms] TO DISK = N'\\backup\upms\Upms_log_20260927_1315.trn'
  WITH COMPRESSION, CHECKSUM, INIT;
```

Schedule these with SQL Server Agent jobs or the company's backup tool, and alert on any failed or missed
job. Once a week, `RESTORE VERIFYONLY ... WITH CHECKSUM` the latest full backup.

## Restore runbook

Use this when the database is lost or damaged, or when data must be recovered to a point in time.

1. **Declare the incident** and note the time. Put the app in maintenance (stop the container or the IIS site)
   so nobody writes to a damaged database.
2. **Choose the target time**: the moment before the failure, or "latest" for a lost server.
3. **Prepare a SQL Server** of the same or a newer version (a new server if the old one is lost).
4. **Take a tail-log backup** if the old database is still reachable, so no committed work is lost:
   `BACKUP LOG [Upms] TO DISK = N'...\Upms_tail.trn' WITH NORECOVERY, NO_TRUNCATE;`
5. **Restore** the last full backup before the target time, then the last differential after it, then every log
   backup in order, stopping at the target time:

   ```sql
   RESTORE DATABASE [Upms] FROM DISK = N'...\Upms_full_20260927.bak' WITH NORECOVERY, REPLACE, STATS = 10;
   RESTORE DATABASE [Upms] FROM DISK = N'...\Upms_diff_20260927_1300.bak' WITH NORECOVERY;
   RESTORE LOG [Upms] FROM DISK = N'...\Upms_log_20260927_1315.trn' WITH NORECOVERY;
   -- ... each later log backup ...
   RESTORE LOG [Upms] FROM DISK = N'...\Upms_log_20260927_1400.trn' WITH STOPAT = '2026-09-27T13:52:00', RECOVERY;
   ```

6. **Check the database**: `DBCC CHECKDB (N'Upms') WITH NO_INFOMSGS;` must report no errors. Recreate the
   application login and its database user if the server is new.
7. **Point the app at it**: update `ConnectionStrings__Default` if the server changed; keep the same data
   protection certificate (otherwise everyone is signed out, which is acceptable but noisy).
8. **Start the app** and confirm `/health/ready` returns 200. Sign in, open the project list, a board and a
   task drawer, and check that the latest work before the target time is present.
9. **Close the incident**: record the times below and tell users which period, if any, needs re-entering.

## Restore drill

Run the drill before go-live and then every quarter, on a separate server, using the real backups:

1. Pick a recent moment and note it as the target time.
2. Follow steps 3–8 of the runbook against a new database name (for example `Upms_Drill`).
3. Start a separate app instance pointed at it and check the most recent changes before the target time.
4. Fill in the record below. The drill passes when data loss ≤ 1 hour and total time ≤ 4 hours.

### Drill record

| Field | Value |
|-------|-------|
| Date and people | |
| Backups used (full / differential / last log) | |
| Target time | |
| Latest change found after the restore (task key, time) | |
| Data loss (target time − latest change) | ≤ 1 hour? |
| Restore started / finished | |
| App started and checked | |
| Total time | ≤ 4 hours? |
| Problems and follow-ups | |
