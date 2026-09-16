# Threat model

This document describes what Aegis-ID protects, who it trusts, what it
detects, and — just as importantly — what it does not. It is written for
the same audience as the control catalog: an administrator deciding whether
to run this tool against a production tenant, and a reviewer deciding
whether to trust its output.

## Non-negotiable design principle

Aegis-ID never requests a Microsoft Graph **write** scope. It reads,
correlates, and reports. It modifies nothing, deletes nothing, creates
nothing. This is enforced by convention today (`WellKnownPermissions.Required`
lists only `.Read` scopes, and `aegis doctor` warns if a write scope was
granted to the app registration by mistake) — a future automated test
asserting this property (planned as part of US-019's test suite) is the
guard-rail against regression.

## Assets

| Asset | Sensitivity | Where it lives |
|---|---|---|
| Microsoft Graph client secret | High — grants read access to the whole tenant's identity data | Passed via `--secret` or `AEGIS_CLIENT_SECRET`; held only in process memory, via `Azure.Identity.ClientSecretCredential`. Never logged, never written to disk by Aegis-ID. |
| `TenantSnapshot` (live or dumped via `--dump`) | High — contains UPNs, display names, role assignments, registered MFA method *types* (not secrets), Conditional Access policy shape, and application permission grants | Local file, if `--dump` is used. Not encrypted at rest by Aegis-ID; the operator is responsible for where it's stored. |
| JSON/CSV/console reports | Medium-high — a curated list of the tenant's exact weaknesses | Local files (`--file <path>`) or stdout. This is the single most attractive target for an attacker: it is, by construction, a prioritized attack plan against the tenant. |
| `aegis-suppressions.yaml` | Low — documents accepted risk, not a secret itself | Local file, typically versioned alongside the audit workflow. |

## Actors

| Actor | Trust level | Notes |
|---|---|---|
| The operator running Aegis-ID (Amina, Karim, or a CI pipeline as Lucie) | Trusted | Holds the client secret and the resulting reports. Aegis-ID assumes the machine it runs on is not already compromised. |
| The audited tenant (Microsoft Graph) | Trusted as a data source | Aegis-ID trusts Graph API responses over TLS; it does not independently verify tenant configuration through a second channel. |
| Anyone who obtains a generated report or snapshot | Untrusted / adversarial | A report or `--dump` file handed to, or stolen by, the wrong person is a ready-made target list. Treat these files with at least the confidentiality of the findings they contain. |
| A malicious or compromised application already in the tenant | Untrusted | Out of Aegis-ID's control; IAM-006/IAM-007/IAM-008 exist specifically to surface this risk to the operator, not to contain it. |

## Trust assumptions

- The host machine running the CLI, and its file system, are trusted. Aegis-ID does not encrypt its own output.
- The `AEGIS_CLIENT_SECRET` value is only as safe as how the operator supplies it; CI pipelines should inject it as a masked secret, never a plain build argument.
- TLS to `https://graph.microsoft.com` is trusted; Aegis-ID performs no additional certificate pinning.
- `--redact` (pseudonymizing UPNs/display names in a dumped snapshot, per US-005) is not implemented yet — until then, a `--dump` file is exactly as sensitive as the tenant's live directory.

## What Aegis-ID detects

Point-in-time identity and access misconfigurations in Microsoft Entra ID:
absent or weak MFA on privileged accounts, standing (non-PIM) privileged
role assignments, Global Administrator count out of range, an absent or
non-compliant break-glass account, expiring/non-expiring application
credentials, high-risk application Graph permissions, unrestricted user
consent, stale service principals, unenforced legacy-auth blocking,
disabled/report-only Conditional Access policies, permanent CA policy
exclusions, open application registration, privileged guest accounts, open
guest invitation, and stale user accounts. See the 15-row table in the
README, and one `docs/controls/IAM-XXX.md` per control for exact logic and
known false positives.

## What Aegis-ID does not detect

- **Anything in motion.** Every finding reflects the tenant at the moment
  of the scan. There is no real-time alerting or continuous monitoring
  (explicitly out of scope, backlog §6).
- **Workload-level misconfiguration.** Exchange, SharePoint, and Teams
  settings are not audited — only identity and access.
- **Azure RBAC** at the subscription or resource level.
- **An attack already in progress.** Aegis-ID is a posture auditor, not an
  intrusion detection system; it has no visibility into sign-in anomalies
  beyond the aggregate staleness signals IAM-008/IAM-015 use.
- **Conditional Access nuance beyond block/MFA/state.** Device compliance
  requirements, risk-based conditions, named locations, and session
  controls are not evaluated by any current control.
- **Anything about Aegis-ID's own operators.** There is no user or role
  management within the tool itself (out of scope by design).

## Reporting a vulnerability

See `SECURITY.md`.
