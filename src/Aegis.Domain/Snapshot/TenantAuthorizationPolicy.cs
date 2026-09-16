namespace Aegis.Domain.Snapshot;

public enum UserConsentPolicy
{
    Disabled,
    AllowForVerifiedPublishersLowRisk,
    AllowForAny,
}

public enum GuestInvitePolicy
{
    Nobody,
    OnlyAdminsAndInviters,
    AdminsInvitersAndMembers,
    Everyone,
}

/// <summary>Tenant-wide settings from the Graph `policies/authorizationPolicy` singleton.</summary>
public sealed record TenantAuthorizationPolicy(
    UserConsentPolicy UserConsentForApps,
    bool UsersCanRegisterApplications,
    GuestInvitePolicy GuestInviteRestriction);
