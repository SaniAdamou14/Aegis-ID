# Aegis-ID

**Read-only security posture auditor for Microsoft Entra ID tenants.**

> Status: early scaffold. This README is a placeholder — the full version
> (demo GIF, control catalog, quickstart, permissions, architecture, known
> limitations) will be written as part of US-021 once the control catalog
> and CLI stabilize.

## Non-negotiable design principle

Aegis-ID never requests Microsoft Graph write scopes. It reads, correlates,
and reports. It modifies nothing, deletes nothing, creates nothing.

## Current state

- Domain model (`TenantSnapshot`, `Finding`, control engine, scoring) — in progress.
- All 15 v1 IAM controls implemented: IAM-001 through IAM-015.
- CLI, offline (no network, no tenant needed):
  - `aegis demo` — synthetic embedded snapshot.
  - `aegis evaluate --from <snapshot.json> [--fail-on <Severity>]`
- CLI, live Microsoft Graph (client credentials flow, US-001/003/004):
  - `aegis doctor --tenant-id <id> --client-id <id> [--secret <secret>]` —
    reports which of the six required read-only permissions are granted,
    and warns if a write scope was granted by mistake.
  - `aegis scan --tenant-id <id> --client-id <id> [--secret <secret>]
    [--fail-on <Severity>] [--dump <snapshot.json>]` — collects a live
    tenant snapshot (paginated, retries on 429/5xx with backoff + jitter)
    and evaluates it with the same engine as `evaluate`.
  - The live collection layer (`src/Aegis.Graph`) is unit-tested against
    fixture HTTP responses, but has not yet been validated end-to-end
    against a real tenant — see `docs/dev-tenant-setup.md`.
  - Client secret: `--secret`, or the `AEGIS_CLIENT_SECRET` environment
    variable. Never logged, never printed.

See `Aegis-ID_Product_Backlog.md` for the full product backlog.

## Development

```bash
dotnet build AegisId.sln
dotnet test
dotnet run --project src/Aegis.Cli -- demo
```
