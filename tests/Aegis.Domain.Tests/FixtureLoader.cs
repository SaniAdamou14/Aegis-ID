using Aegis.Domain.Snapshot;

namespace Aegis.Domain.Tests;

internal static class FixtureLoader
{
    public static TenantSnapshot Load(string fileName) =>
        TenantSnapshotSerializer.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
}
