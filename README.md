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
- Controls implemented so far: IAM-001, IAM-003, IAM-005, IAM-006, IAM-009.
- CLI: `aegis evaluate --from <snapshot.json>`, `aegis demo` (offline, no
  network calls, uses a synthetic snapshot).
- Live Microsoft Graph authentication and collection are not implemented
  yet — everything runs today against snapshot files.

See `Aegis-ID_Product_Backlog.md` for the full product backlog.

## Development

```bash
dotnet build AegisId.sln
dotnet test
dotnet run --project src/Aegis.Cli -- demo
```
