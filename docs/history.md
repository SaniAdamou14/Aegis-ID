# Scan history (`--db`) and `aegis diff`

Opt-in local scan history (US-017/US-018) — nothing is persisted unless you
pass `--db`.

## Recording a scan

```bash
aegis evaluate --from snapshot.json --db aegis-history.db
aegis scan --tenant-id <id> --client-id <id> --db aegis-history.db
```

Each run adds one row to a local SQLite database (created if it doesn't
exist) and prints the scan's id:

```
Saved scan 5c0c1de7-507b-415e-840c-74911cb45014 to 'aegis-history.db' (retention: 90 days).
```

The stored row is: tenant name, timestamp, posture score, and the same
versioned JSON document `--output json` produces (see
`docs/report-schema.md`) — no secret or token is ever part of it, the same
guarantee that already holds for `--output json`/`csv`.

`--retention-days <n>` (default `90`) controls how long history is kept —
every save also deletes rows older than the retention window from that
database file.

**PostgreSQL** is the option the backlog names for a shared/team setup, but
isn't wired up yet — the SQLite path covers the default, single-operator
use case first. Swapping providers is a small change (EF Core is already
provider-agnostic) rather than a rewrite.

## Comparing two scans

```bash
aegis diff --db aegis-history.db --against <scanId>
```

Compares the **most recently recorded scan** in that database against the
scan identified by `--against` (a scan id printed by an earlier `evaluate
--db`/`scan --db`). A finding is matched across the two by
(`ControlId`, `ObjectId`) — a severity change on the same object counts as
unchanged, not appeared+resolved.

```
Current:  52a0b498-9859-46b7-9fe0-27878771e58c — 2026-09-17 15:03:34Z — score 0/100
Baseline: 5c0c1de7-507b-415e-840c-74911cb45014 — 2026-09-17 15:02:33Z — score 0/100

Appeared: 1
  [Critical] IAM-001 new.admin@demo.aegis-id.dev: Roles: Global Administrator. Registered methods: no authentication method registered.

Resolved: 0

Unchanged: 19
```

Exit code `1` if at least one **appeared** finding is `Critical` or `High`
severity, `0` otherwise; `2` is reserved for execution errors (missing
database file, unknown scan id, `--against` pointing at the latest scan
itself).

A finding that is merely suppressed (`aegis-suppressions.yaml`) between the
two scans still counts as unchanged — suppression documents an accepted
exception, it doesn't mean the underlying condition was fixed.

## Dashboard: score-over-time chart

`Aegis.Api` exposes `GET /api/scans/history?limit=10` (oldest first), read
by the Overview page's chart. Point it at the same file `--db` writes to:

```json
// src/Aegis.Api/appsettings.json
{
  "Persistence": { "DbPath": "aegis-history.db" }
}
```

or the `Persistence__DbPath` environment variable (the double underscore
is ASP.NET Core's config-section separator). If that file doesn't exist
yet, the endpoint returns an empty array and the chart shows its own
empty state rather than an error — recording history is opt-in, so an
empty history is a normal state, not a failure.
