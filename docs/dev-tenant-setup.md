# Setting up the development tenant

This is a manual, one-time setup you do in the browser — it cannot be
scripted or automated from here. `aegis doctor` and `aegis scan` (US-001/003/004)
are already implemented and unit-tested against fixture HTTP responses, but
have never been run against a real tenant. Once this setup is done, that's
the one thing left to validate.

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

To also try `aegis scan --interactive` (US-002, device code sign-in with
your own admin account instead of the client secret): **Authentication** →
enable **Allow public client flows**. No redirect URI or secret needed for
that flow — only the client ID from step 2.

## 5. Store credentials locally — never in chat, never in git

`tenantId` and `clientId` are not secret — pass them as CLI flags. The
client secret is: set it via the `AEGIS_CLIENT_SECRET` environment variable
(never printed, never logged) rather than typing it on the command line
where it could end up in shell history.

```bash
export AEGIS_CLIENT_SECRET="<client-secret>"   # PowerShell: $env:AEGIS_CLIENT_SECRET = "<client-secret>"

dotnet run --project src/Aegis.Cli -- doctor \
  --tenant-id "<tenant-id>" --client-id "<client-id>"

dotnet run --project src/Aegis.Cli -- scan \
  --tenant-id "<tenant-id>" --client-id "<client-id>" --fail-on Critical
```

`.gitignore` already excludes `*.env` and `appsettings.Local.json` if you
prefer to keep credentials in a local dotenv-style file instead.
