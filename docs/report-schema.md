# Aegis-ID JSON report schema

**Current version: `1.0`.** Produced by `aegis evaluate --output json --file
<path>` and `aegis scan --output json --file <path>` (US-010). The version
appears in the `schemaVersion` field of every report; a breaking change to
this shape bumps that value and is called out in the changelog.

All property names are camelCase. Enum values (`status`, `severity`) are
serialized as lowercase strings.

## Top level

| Field | Type | Description |
|---|---|---|
| `schemaVersion` | string | This document's version, e.g. `"1.0"`. |
| `tenant` | string | Tenant display name. |
| `evaluatedAt` | string (ISO 8601) | When the scan was evaluated. |
| `postureScore` | integer | 0–100, see `PostureScoreCalculator`. |
| `severityCounts` | object | `critical`/`high`/`medium`/`low`/`info` counts, excluding findings marked as an expected exception. |
| `controls` | array of [Control](#control) | One entry per discovered control, regardless of outcome. |

## Control

| Field | Type | Description |
|---|---|---|
| `controlId` | string | e.g. `"IAM-001"`. |
| `title` | string | Human-readable control title. |
| `status` | `"passed" \| "failed" \| "skipped" \| "error"` | |
| `skipReason` | string or null | Set when `status` is `skipped`. |
| `errorMessage` | string or null | Set when `status` is `error`. |
| `findings` | array of [Finding](#finding) | Empty when `status` is `passed`, `skipped`, or `error`. |

## Finding

| Field | Type | Description |
|---|---|---|
| `controlId` | string | |
| `severity` | `"critical" \| "high" \| "medium" \| "low" \| "info"` | |
| `objectType` | string | e.g. `"User"`, `"Application"`, `"Tenant"`, `"ConditionalAccessPolicy"`. |
| `objectId` | string | |
| `objectName` | string | |
| `evidence` | string | What was observed. |
| `riskDescription` | string | Why it matters. |
| `remediation` | array of string | Ordered remediation steps. |
| `cisReference` | string or null | CIS Microsoft 365 Benchmark section. |
| `mitreTechnique` | string or null | MITRE ATT&CK technique ID. |
| `detectedAt` | string (ISO 8601) | |
| `isExpectedException` | boolean | e.g. a documented break-glass account. |

## Example

```json
{
  "schemaVersion": "1.0",
  "tenant": "Aegis-ID Demo Tenant",
  "evaluatedAt": "2026-09-16T19:09:43.9360496+00:00",
  "postureScore": 0,
  "severityCounts": { "critical": 3, "high": 10, "medium": 5, "low": 0, "info": 0 },
  "controls": [
    {
      "controlId": "IAM-001",
      "title": "Privileged accounts without strong MFA registered",
      "status": "failed",
      "skipReason": null,
      "errorMessage": null,
      "findings": [
        {
          "controlId": "IAM-001",
          "severity": "critical",
          "objectType": "User",
          "objectId": "u-001",
          "objectName": "alice.admin@demo.aegis-id.dev",
          "evidence": "Roles: Global Administrator. Registered methods: no authentication method registered.",
          "riskDescription": "Privileged account has no strong MFA method registered, making it a prime target for credential compromise.",
          "remediation": [
            "Register a FIDO2 security key, Windows Hello for Business, or the Microsoft Authenticator app for this account.",
            "Enforce phishing-resistant MFA for all privileged roles via a Conditional Access authentication strengths policy."
          ],
          "cisReference": "1.1.1",
          "mitreTechnique": "T1078.004",
          "detectedAt": "2026-09-16T19:09:43.9360496+00:00",
          "isExpectedException": false
        }
      ]
    }
  ]
}
```

## CSV export

`--output csv --file <path>` (US-011) writes one row per finding, UTF-8
encoded with a leading BOM for Excel compatibility. Columns: `ControlId`,
`Severity`, `ObjectType`, `ObjectId`, `ObjectName`, `Evidence`,
`Remediation` (steps joined with `; `), `CisReference`, `MitreTechnique`,
`DetectedAt`. Fields containing a comma, quote, or newline are quoted per
RFC 4180.
