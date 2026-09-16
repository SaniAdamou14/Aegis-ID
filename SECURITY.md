# Security policy

## Supported versions

Aegis-ID is pre-v1 (no tagged release yet). Security fixes land on
`master`; there is no older version to backport to.

## Reporting a vulnerability

Please **do not** open a public GitHub issue for a suspected vulnerability.

Instead, use [GitHub's private security advisory feature](https://github.com/SaniAdamou14/Aegis-ID/security/advisories/new)
for this repository. This lets you describe the issue, and the maintainer
to respond and coordinate a fix, without the report being public before a
patch is available.

Please include:

- The version or commit SHA you tested against.
- Steps to reproduce, or a minimal example.
- The impact you believe the issue has.

## Scope

Aegis-ID's core design principle — it never requests a Microsoft Graph
write scope — is itself a security control (see `docs/threat-model.md`).
Reports that a control requests, or could be tricked into requesting, a
write scope are treated as high-severity by default, as are any issue that
could cause a client secret, tenant data, or a generated report to leak
somewhere it shouldn't (e.g. into logs, into a file the tool didn't intend
to write, or over an unencrypted channel).

Out of scope: vulnerabilities in Microsoft Graph itself, or in a tenant's
own configuration that Aegis-ID is designed to *report on* (that's the
product working as intended, not a bug in Aegis-ID).
