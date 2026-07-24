# Setting up the development tenant

This is a manual, one-time setup you do in the browser — it cannot be
scripted or automated from here. Once done, Aegis-ID's real Microsoft
Graph authentication (US-001/002/003/004) can be implemented against a
live tenant instead of only fixture snapshots.

## 1. Create the Microsoft 365 Developer Program tenant

1. Go to <https://developer.microsoft.com/microsoft-365/dev-program> and
   sign up with a Microsoft account. Prefer a dedicated account rather
   than a personal one — you'll be Global Administrator on this tenant.
2. Choose **Instant sandbox** (E5, 25 licenses, provisioned in minutes).
3. Note the initial domain (`*.onmicrosoft.com`) and the admin
   credentials you're asked to set.

## 2. Get the Tenant ID

In <https://portal.azure.com>, sign in with the admin account, open
**Microsoft Entra ID** → **Overview**, and copy the **Tenant ID**.

## 3. Seed the tenant with a deliberately misconfigured baseline

Before creating the Aegis-ID app registration, run
[`scripts/provision-demo-tenant.ps1`](../scripts/provision-demo-tenant.ps1)
(requires `Install-Module Microsoft.Graph -Scope CurrentUser`, then an
interactive Global Administrator sign-in). It creates ~30 baseline users,
3 privileged accounts (one intentionally without MFA), two applications
with excessive Graph permissions and poor credential hygiene, two
Conditional Access policies (one active, one report-only), and two guest
invitations. See the script header for exactly what it does and the
manual follow-ups it prints at the end.

This script is **admin tooling, not part of Aegis-ID** — it requires
write access on purpose, because its entire job is to misconfigure the
tenant that Aegis-ID will later audit read-only. Keep it out of any
credentials used by Aegis-ID itself.

## 4. Register the Aegis-ID application (read-only)

1. Entra ID → **App registrations** → **New registration**.
   - Name: `Aegis-ID`.
   - Supported account types: single tenant.
   - No redirect URI (client credentials flow, no interactive sign-in).
2. Note the **Application (client) ID**.
3. **Certificates & secrets** → **New client secret** → copy the value
   immediately (shown once).
4. **API permissions** → **Add a permission** → **Microsoft Graph** →
   **Application permissions** → add exactly these six:
   - `Directory.Read.All`
   - `Policy.Read.All`
   - `AuditLog.Read.All`
   - `Application.Read.All`
   - `RoleManagement.Read.Directory`
   - `UserAuthenticationMethod.Read.All`
5. Click **Grant admin consent**. Double-check no write scope was added —
   this is the non-negotiable design principle of the project.

## 5. Store credentials locally — never in chat, never in git

```bash
dotnet user-secrets init --project src/Aegis.Cli
dotnet user-secrets set "Aegis:TenantId" "<tenant-id>" --project src/Aegis.Cli
dotnet user-secrets set "Aegis:ClientId" "<client-id>" --project src/Aegis.Cli
dotnet user-secrets set "Aegis:ClientSecret" "<client-secret>" --project src/Aegis.Cli
```

Or, for CI-style usage, the `AEGIS_CLIENT_SECRET` environment variable
(see US-001). `.gitignore` already excludes `*.env` and
`appsettings.Local.json`.
