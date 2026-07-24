namespace Aegis.Domain.Snapshot;

public enum CredentialType
{
    Secret,
    Certificate,
}

public sealed record ApplicationCredential(CredentialType Type, DateTimeOffset? ExpiresOn);
