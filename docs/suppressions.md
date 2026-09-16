# Suppressions (`aegis-suppressions.yaml`)

Documents an accepted exception for a single finding, identified by control
ID and object ID (US-008). A suppression never hides a finding silently: it
is still listed in every report, tagged `[suppressed: <reason>]`, and
counted separately from `Passed` — the control itself still reports
`Failed`.

## Format

A YAML list at the top level. Each entry:

| Field | Required | Description |
|---|---|---|
| `controlId` | yes | e.g. `IAM-005`. |
| `objectId` | yes | The `ObjectId` of the finding (see the JSON/CSV report). |
| `reason` | yes | Free text. A missing `reason` fails the entire file to load. |
| `expires` | no | An ISO date. Once passed, the suppression stops applying, the finding reappears in full, and the CLI prints a warning naming the expired suppression. |

```yaml
- controlId: IAM-005
  objectId: app-002
  reason: "Rotation scheduled, ticket OPS-42"
  expires: 2026-12-31
- controlId: IAM-004
  objectId: u-004
  reason: "Documented break-glass account, reviewed quarterly"
```

## Usage

```bash
aegis evaluate --from snapshot.json --suppressions aegis-suppressions.yaml
aegis scan --tenant-id <id> --client-id <id> --suppressions aegis-suppressions.yaml
```

A suppressed finding's severity is excluded from the posture score and from
the console severity counts, but remains visible in the console, JSON, and
CSV output so the exception stays auditable.
