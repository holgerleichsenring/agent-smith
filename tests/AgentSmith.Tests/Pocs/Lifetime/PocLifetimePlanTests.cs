using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: what the lifetime pass does for one PoC, decided in memory.</summary>
public sealed class PocLifetimePlanTests
{
    private static readonly DateTimeOffset Created = PocLifetimeTests.Created;
    private static readonly DateTimeOffset Expiry = Created.AddDays(90);
    private static readonly PocTargetConfig Target = new() { Name = "main", LifetimeDays = 90, ReminderDays = [14, 7, 1] };
    private static readonly PocLifetimeConfig Config = new();
    private readonly PocLifetimePlan _plan = new();

    internal static PocLifetimeSubject Subject(
        string state = PocStates.Ready, int replicas = 1, int? revision = 1, DateTimeOffset? seen = null, long? expiredAt = null,
        bool uploadGone = false, long? expiresAt = null) =>
        new(7, "shop", "main", state, replicas, revision, new PocLifetimeFacts(Created, expiresAt, seen?.UtcTicks, 0), expiredAt, uploadGone, "run-1");

    private string? Case(PocLifetimeSubject poc, DateTimeOffset now) =>
        _plan.CaseOf(poc, new PocLifetime().Of(poc.Lifetime, Target, now), Config, now);

    [Fact]
    public void CaseOf_ExpiredAndSeen_Deletes()
    {
        Case(Subject(seen: Expiry.AddDays(-1)), Expiry.AddHours(1)).Should().Be(PocLifetimeCases.SeenDelete);
    }

    [Fact]
    public void CaseOf_ExpiredUnseen_Stops_AlsoAtZeroReplicas_WhenNoExpiryStopSinceTheExpiry()
    {
        Case(Subject(), Expiry.AddHours(1)).Should().Be(PocLifetimeCases.ExpiryStop);
        Case(Subject(PocStates.Stopped, 0), Expiry.AddHours(1)).Should().Be(PocLifetimeCases.ExpiryStop);
    }

    [Fact]
    public void CaseOf_AnExpiryStop29DaysOld_Waits_31DaysOld_Deletes()
    {
        var stop = Expiry.AddHours(1).UtcTicks;

        Case(Subject(PocStates.Stopped, 0, expiredAt: stop), Expiry.AddDays(29)).Should().BeNull();
        Case(Subject(PocStates.Stopped, 0, expiredAt: stop), Expiry.AddDays(31)).Should().Be(PocLifetimeCases.UnseenDelete);
    }

    [Fact]
    public void CaseOf_AnExpiryStopFromBeforeAnExtension_DoesNotCount()
    {
        var extended = Expiry.AddDays(90);
        var poc = Subject(PocStates.Stopped, 0, expiredAt: Expiry.AddHours(1).UtcTicks, expiresAt: extended.UtcTicks);

        Case(poc, extended.AddDays(40)).Should().Be(PocLifetimeCases.ExpiryStop, "the 30 days count from a stop of THIS expiry");
        Case(poc, extended.AddDays(-1)).Should().BeNull("not expired");
    }

    [Fact]
    public void CaseOf_BlockedNeverDeployed_RemovedOnlyOnceItsUploadIsGone()
    {
        Case(Subject(PocStates.Blocked, revision: null, uploadGone: true), Created.AddDays(8)).Should().Be(PocLifetimeCases.Removal);
        Case(Subject(PocStates.Blocked, revision: null), Created.AddDays(8)).Should().BeNull("the upload is kept");
        Case(Subject(PocStates.Importing, revision: null, uploadGone: true), Created.AddDays(200)).Should().BeNull();
        Case(Subject(PocStates.Ready, revision: null), Created.AddDays(200)).Should().BeNull("a pending delete");
    }

    [Fact]
    public void Skips_ALiveRun_AFailedStopOfTwoHours_NotOneOf25Hours_NorAFailedDeploy()
    {
        var now = Expiry.AddDays(1);
        PocLastRun Failed(string pipeline, double hours) => new("run-1", pipeline, RunStatuses.Failed, now.AddHours(-hours));

        _plan.Skips(null, true, now).Should().BeTrue();
        _plan.Skips(Failed(PipelinePresets.PocStopName, 2), false, now).Should().BeTrue();
        _plan.Skips(Failed(PipelinePresets.PocDeleteName, 2), false, now).Should().BeTrue();
        _plan.Skips(Failed(PipelinePresets.PocStopName, 25), false, now).Should().BeFalse();
        _plan.Skips(Failed(PipelinePresets.PocDeployName, 2), false, now).Should().BeFalse();
        _plan.Skips(null, false, now).Should().BeFalse();
    }

    [Fact]
    public void Reasons_NameTheValueAndTheKey()
    {
        var pocs = new PocsConfig();
        var reasons = new PocLifetimeReasons();

        reasons.Of(PocLifetimeCases.ExpiryStop, pocs).Should().Be(new PocAuditAct(PocActs.Expired, "stopped, not deleted: no reminder was seen before expiry"));
        reasons.Of(PocLifetimeCases.UnseenDelete, pocs).Reason.Should().Contain("30 days").And.Contain(PocLifetimeConfig.UnseenDeleteAfterDaysKey);
        reasons.Of(PocLifetimeCases.Removal, pocs).Should().Match<PocAuditAct>(a => a.Act == PocActs.Deleted
            && a.Reason.Contains("7 days") && a.Reason.Contains(PocsConfig.BlockedRetentionDaysKey));
    }
}
