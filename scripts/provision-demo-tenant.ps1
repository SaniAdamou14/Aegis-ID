<#
.SYNOPSIS
    Seeds a fresh Microsoft 365 Developer tenant with a deliberately
    misconfigured baseline so Aegis-ID has real findings to detect.

.DESCRIPTION
    This is admin tooling, NOT part of Aegis-ID. It authenticates
    interactively as a Global Administrator (delegated permissions) and
    creates/modifies directory objects. Aegis-ID itself never requests
    write scopes — this script is what deliberately misconfigures the
    tenant that Aegis-ID will later audit read-only.

    Run this once against a brand new Microsoft 365 Developer Program
    tenant, right after signup, before creating the Aegis-ID app
    registration described in docs/dev-tenant-setup.md.

.PREREQUISITES
    Install-Module Microsoft.Graph -Scope CurrentUser

.NOTES
    Idempotency: re-running is safe for users/apps (skipped if a user with
    the same UPN or an app with the same display name already exists) but
    Conditional Access policies are matched by display name too.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TenantDomain,  # e.g. contoso12345.onmicrosoft.com

    [int]$RegularUserCount = 30
)

$ErrorActionPreference = "Stop"

$requiredScopes = @(
    "User.ReadWrite.All",
    "Application.ReadWrite.All",
    "Policy.ReadWrite.ConditionalAccess",
    "RoleManagement.ReadWrite.Directory",
    "Directory.ReadWrite.All"
)

Write-Host "Connecting to Microsoft Graph (interactive sign-in as Global Administrator)..." -ForegroundColor Cyan
Connect-MgGraph -Scopes $requiredScopes -NoWelcome

function New-DemoPassword {
    # Meets default Entra ID complexity requirements; users are demo-only.
    return "Aegis-Demo!" + (Get-Random -Minimum 1000 -Maximum 9999)
}

function Get-OrCreateUser {
    param(
        [string]$UserPrincipalName,
        [string]$DisplayName
    )

    $existing = Get-MgUser -Filter "userPrincipalName eq '$UserPrincipalName'" -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "  User already exists: $UserPrincipalName" -ForegroundColor DarkGray
        return $existing
    }

    $passwordProfile = @{
        Password                      = New-DemoPassword
        ForceChangePasswordNextSignIn = $false
    }

    $user = New-MgUser -UserPrincipalName $UserPrincipalName `
        -DisplayName $DisplayName `
        -MailNickname ($UserPrincipalName.Split('@')[0]) `
        -AccountEnabled `
        -PasswordProfile $passwordProfile

    Write-Host "  Created user: $UserPrincipalName" -ForegroundColor Green
    return $user
}

function Add-DirectoryRoleMember {
    param(
        [string]$RoleDisplayName,
        [string]$UserId
    )

    $role = Get-MgDirectoryRole -Filter "displayName eq '$RoleDisplayName'" -ErrorAction SilentlyContinue
    if (-not $role) {
        # Directory roles must be activated from their role template before first use.
        $template = Get-MgDirectoryRoleTemplate | Where-Object { $_.DisplayName -eq $RoleDisplayName }
        $role = New-MgDirectoryRole -RoleTemplateId $template.Id
    }

    $alreadyMember = Get-MgDirectoryRoleMember -DirectoryRoleId $role.Id |
        Where-Object { $_.Id -eq $UserId }

    if (-not $alreadyMember) {
        New-MgDirectoryRoleMemberByRef -DirectoryRoleId $role.Id -BodyParameter @{
            "@odata.id" = "https://graph.microsoft.com/v1.0/directoryObjects/$UserId"
        }
        Write-Host "  Assigned role '$RoleDisplayName'" -ForegroundColor Green
    }
}

# --- 1. Regular users (baseline population) -----------------------------

Write-Host "`n=== Creating $RegularUserCount regular users ===" -ForegroundColor Cyan
$firstNames = @("Alice","Bob","Carol","Dave","Erin","Frank","Grace","Heidi","Ivan","Judy")
$lastNames  = @("Martin","Bernard","Dubois","Thomas","Robert","Petit","Durand","Leroy","Moreau","Simon")

for ($i = 1; $i -le $RegularUserCount; $i++) {
    $first = $firstNames[(Get-Random -Maximum $firstNames.Count)]
    $last = $lastNames[(Get-Random -Maximum $lastNames.Count)]
    $upn = "$($first.ToLower()).$($last.ToLower())$i@$TenantDomain"
    Get-OrCreateUser -UserPrincipalName $upn -DisplayName "$first $last" | Out-Null
}

# --- 2. Privileged accounts, deliberately under-protected ----------------

Write-Host "`n=== Creating privileged accounts (IAM-001 / IAM-003 targets) ===" -ForegroundColor Cyan

$gaNoMfa = Get-OrCreateUser -UserPrincipalName "alice.admin@$TenantDomain" -DisplayName "Alice Admin (no MFA)"
Add-DirectoryRoleMember -RoleDisplayName "Global Administrator" -UserId $gaNoMfa.Id
# Deliberately do NOT register any MFA method for this account — new users
# have none by default, which is exactly the IAM-001 non-compliant case.

$gaSecondary = Get-OrCreateUser -UserPrincipalName "dave.admin@$TenantDomain" -DisplayName "Dave Admin"
Add-DirectoryRoleMember -RoleDisplayName "Global Administrator" -UserId $gaSecondary.Id

$secAdminWeakMfa = Get-OrCreateUser -UserPrincipalName "carol.security@$TenantDomain" -DisplayName "Carol Security"
Add-DirectoryRoleMember -RoleDisplayName "Security Administrator" -UserId $secAdminWeakMfa.Id
Write-Host "  NOTE: register SMS-only MFA for carol.security manually in the portal" -ForegroundColor Yellow
Write-Host "        (Security info -> Add method -> Phone) to produce the IAM-001 'High' case." -ForegroundColor Yellow

# --- 3. Applications with excessive permissions and bad credential hygiene

Write-Host "`n=== Creating over-privileged applications (IAM-005 / IAM-006 targets) ===" -ForegroundColor Cyan

$graphSpId = (Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'").Id
$graphAppRoles = (Get-MgServicePrincipal -ServicePrincipalId $graphSpId).AppRoles

function Grant-AppRole {
    param([string]$ServicePrincipalId, [string]$RoleName)
    $role = $graphAppRoles | Where-Object { $_.Value -eq $RoleName }
    if (-not $role) { Write-Warning "Graph app role '$RoleName' not found"; return }

    New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $ServicePrincipalId -BodyParameter @{
        principalId = $ServicePrincipalId
        resourceId  = $graphSpId
        appRoleId   = $role.Id
    } | Out-Null
}

$legacyApp = Get-MgApplication -Filter "displayName eq 'Legacy-Sync-Tool'" -ErrorAction SilentlyContinue
if (-not $legacyApp) {
    $legacyApp = New-MgApplication -DisplayName "Legacy-Sync-Tool"
    $legacySp = New-MgServicePrincipal -AppId $legacyApp.AppId
    Grant-AppRole -ServicePrincipalId $legacySp.Id -RoleName "Directory.ReadWrite.All"
    Grant-AppRole -ServicePrincipalId $legacySp.Id -RoleName "Mail.Send"
    # Client secret with NO expiration date set beyond the max, and never rotated —
    # intentionally triggers IAM-005.
    Add-MgApplicationPassword -ApplicationId $legacyApp.Id -PasswordCredential @{
        DisplayName = "legacy-secret"
        EndDateTime = (Get-Date).AddYears(2)
    } | Out-Null
    Write-Host "  Created Legacy-Sync-Tool with Directory.ReadWrite.All + Mail.Send" -ForegroundColor Green
}

$payrollApp = Get-MgApplication -Filter "displayName eq 'Payroll-Integration-App'" -ErrorAction SilentlyContinue
if (-not $payrollApp) {
    $payrollApp = New-MgApplication -DisplayName "Payroll-Integration-App"
    New-MgServicePrincipal -AppId $payrollApp.AppId | Out-Null
    Add-MgApplicationPassword -ApplicationId $payrollApp.Id -PasswordCredential @{
        DisplayName = "payroll-secret"
        EndDateTime = (Get-Date).AddDays(10)   # expiring soon -> IAM-005
    } | Out-Null
    Write-Host "  Created Payroll-Integration-App with a secret expiring in 10 days" -ForegroundColor Green
}

# --- 4. Conditional Access policies (one active, one report-only) --------

Write-Host "`n=== Creating Conditional Access policies (IAM-009 target) ===" -ForegroundColor Cyan

$mfaPolicy = Get-MgIdentityConditionalAccessPolicy -Filter "displayName eq 'Require MFA for admins'" -ErrorAction SilentlyContinue
if (-not $mfaPolicy) {
    New-MgIdentityConditionalAccessPolicy -DisplayName "Require MFA for admins" -State "enabled" `
        -Conditions @{
            Users = @{ IncludeRoles = @() }  # configure target roles manually if needed
            Applications = @{ IncludeApplications = @("All") }
        } `
        -GrantControls @{ Operator = "OR"; BuiltInControls = @("mfa") } | Out-Null
    Write-Host "  Created 'Require MFA for admins' (enabled)" -ForegroundColor Green
}

$legacyAuthPolicy = Get-MgIdentityConditionalAccessPolicy -Filter "displayName eq 'Report only - block legacy auth'" -ErrorAction SilentlyContinue
if (-not $legacyAuthPolicy) {
    New-MgIdentityConditionalAccessPolicy -DisplayName "Report only - block legacy auth" -State "enabledForReportingButNotEnforced" `
        -Conditions @{
            Users = @{ IncludeUsers = @("All") }
            Applications = @{ IncludeApplications = @("All") }
            ClientAppTypes = @("exchangeActiveSync", "other")
        } `
        -GrantControls @{ Operator = "OR"; BuiltInControls = @("block") } | Out-Null
    Write-Host "  Created 'Report only - block legacy auth' (report-only, deliberately not enforced)" -ForegroundColor Green
}

# --- 5. Guest accounts (IAM-013 / IAM-014 targets, future controls) ------

Write-Host "`n=== Inviting guest accounts ===" -ForegroundColor Cyan
$guestEmails = @("guest1.external@example.com", "guest2.external@example.com")
foreach ($email in $guestEmails) {
    $existing = Get-MgUser -Filter "mail eq '$email'" -ErrorAction SilentlyContinue
    if (-not $existing) {
        New-MgInvitation -InvitedUserEmailAddress $email `
            -InviteRedirectUrl "https://myapps.microsoft.com" `
            -SendInvitationMessage:$false | Out-Null
        Write-Host "  Invited guest: $email" -ForegroundColor Green
    }
}

Write-Host "`nDone. Manual follow-ups:" -ForegroundColor Cyan
Write-Host "  1. Register SMS-only MFA for carol.security (see note above)."
Write-Host "  2. Grant admin consent for the app permissions in the portal if the"
Write-Host "     Grant-AppRole calls above did not auto-consent in your tenant."
Write-Host "  3. Proceed to docs/dev-tenant-setup.md step 3 to create the read-only"
Write-Host "     Aegis-ID app registration used for the actual audits."
