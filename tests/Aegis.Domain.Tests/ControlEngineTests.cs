using Aegis.Domain.Controls;
using Aegis.Domain.Findings;
using Aegis.Domain.Snapshot;

namespace Aegis.Domain.Tests;

public class ControlEngineTests
{
    // A fully compliant tenant: enough (but not too many) Global Administrators, all
    // with strong MFA, and an active Conditional Access policy blocking legacy auth.
    private static TenantSnapshot CompliantSnapshot() => new(
        new TenantInfo("tenant-1", "Compliant Tenant"),
        [
            new AegisUser("u-1", "ga1@fixture.test", "GA 1", ["Global Administrator"], [AuthenticationMethodType.Fido2]),
            new AegisUser("u-2", "ga2@fixture.test", "GA 2", ["Global Administrator"], [AuthenticationMethodType.Fido2]),
            new AegisUser(
                "u-3", "breakglass@fixture.test", "Break Glass",
                ["Global Administrator"], [AuthenticationMethodType.Fido2],
                IsBreakGlassAccount: true),
        ],
        [],
        [
            new ConditionalAccessPolicy(
                "ca-1",
                "Block legacy auth",
                ConditionalAccessPolicyState.Enabled,
                ["exchangeActiveSync", "other"],
                ["block"],
                ExcludedUserIds: ["u-3"]),
        ],
        DateTimeOffset.UtcNow);

    [Fact]
    public void DiscoverFrom_FindsAllCatalogIamControls()
    {
        var engine = ControlEngine.DiscoverFrom(typeof(Aegis.Controls.Iam.PrivilegedAccountsWithoutStrongMfaControl).Assembly);
        var result = engine.Run(CompliantSnapshot());

        var ids = result.ControlResults.Select(r => r.ControlId).ToList();
        for (var i = 1; i <= 15; i++)
            Assert.Contains($"IAM-{i:000}", ids);
    }

    [Fact]
    public void Run_ControlWithoutFindings_IsMarkedPassed()
    {
        var engine = ControlEngine.DiscoverFrom(typeof(Aegis.Controls.Iam.PrivilegedAccountsWithoutStrongMfaControl).Assembly);
        var result = engine.Run(CompliantSnapshot());

        Assert.All(result.ControlResults, r => Assert.Equal(ControlStatus.Passed, r.Status));
        Assert.Equal(100, result.PostureScore);
    }

    private sealed class ThrowingControl : IControl
    {
        public string Id => "TEST-ERROR";
        public string Title => "Throwing control";
        public Severity DefaultSeverity => Severity.Info;
        public string? CisReference => null;
        public string? MitreTechnique => null;
        public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot) => throw new InvalidOperationException("boom");
    }

    private sealed class AlwaysFindsControl : IControl
    {
        public string Id => "TEST-FINDS";
        public string Title => "Always finds";
        public Severity DefaultSeverity => Severity.Critical;
        public string? CisReference => null;
        public string? MitreTechnique => null;

        public IReadOnlyList<Finding> Evaluate(TenantSnapshot snapshot) =>
        [
            new Finding("TEST-FINDS", Severity.Critical, "Tenant", "t", "t", "evidence", "risk", [], null, null, DateTimeOffset.UtcNow),
        ];
    }

    [Fact]
    public void Run_ThrowingControl_IsMarkedErrorAndDoesNotBlockOtherControls()
    {
        var engine = new ControlEngine([new ThrowingControl(), new AlwaysFindsControl()]);
        var result = engine.Run(CompliantSnapshot());

        var errored = result.ControlResults.Single(r => r.ControlId == "TEST-ERROR");
        Assert.Equal(ControlStatus.Error, errored.Status);
        Assert.Contains("boom", errored.ErrorMessage);

        var found = result.ControlResults.Single(r => r.ControlId == "TEST-FINDS");
        Assert.Equal(ControlStatus.Failed, found.Status);
        Assert.Single(found.Findings);
    }
}
