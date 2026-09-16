# Aegis-ID

[![CI](https://github.com/SaniAdamou14/Aegis-ID/actions/workflows/ci.yml/badge.svg)](https://github.com/SaniAdamou14/Aegis-ID/actions/workflows/ci.yml)

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
  - `aegis evaluate --from <snapshot.json> [--fail-on <Severity>]
    [--output console|json|csv] [--file <path>]`
- CLI, live Microsoft Graph (client credentials flow, US-001/003/004):
  - `aegis doctor --tenant-id <id> --client-id <id> [--secret <secret>]` —
    reports which of the six required read-only permissions are granted,
    and warns if a write scope was granted by mistake.
  - `aegis scan --tenant-id <id> --client-id <id> [--secret <secret>]
    [--fail-on <Severity>] [--dump <snapshot.json>]
    [--output console|json|csv] [--file <path>]` — collects a live
    tenant snapshot (paginated, retries on 429/5xx with backoff + jitter)
    and evaluates it with the same engine as `evaluate`.
  - The live collection layer (`src/Aegis.Graph`) is unit-tested against
    fixture HTTP responses, but has not yet been validated end-to-end
    against a real tenant — see `docs/dev-tenant-setup.md`.
  - Client secret: `--secret`, or the `AEGIS_CLIENT_SECRET` environment
    variable. Never logged, never printed.
- `--output json` writes a versioned JSON report (see `docs/report-schema.md`)
  and `--output csv` writes one row per finding — both require `--file
  <path>`. `--fail-on <Severity>` returns exit code `1` if a finding at or
  above that severity exists, `0` otherwise; exit code `2` is reserved for
  execution errors.
- `--suppressions <file.yaml>` documents accepted exceptions for specific
  findings without hiding them from the report — see `docs/suppressions.md`.
- `--quiet` limits console output to the score and severity counts;
  `--verbose` adds per-control durations (and, for `scan`, each Graph HTTP
  call).

See `Aegis-ID_Product_Backlog.md` for the full product backlog.

## Continuous integration & releases

Every push and pull request runs `.github/workflows/ci.yml`: restore, build
with warnings as errors, `dotnet format --verify-no-changes`, the full test
suite, and a [gitleaks](https://github.com/gitleaks/gitleaks) secret scan.
A tagged push (`vX.Y.Z`) runs `.github/workflows/release.yml`, which
publishes self-contained single-file binaries for Linux, Windows, and macOS
to the GitHub release, and a container image to
`ghcr.io/saniadamou14/aegis-id`.

A branch ruleset on `master` requires the `build-and-test` and `gitleaks`
checks to pass before a push or merge is accepted.

Known gap: there is no coverage badge yet — that needs a third-party
coverage service (e.g. Codecov) wired to an account, not just a workflow
file.

## Development

```bash
dotnet build AegisId.sln
dotnet test
dotnet run --project src/Aegis.Cli -- demo
```
