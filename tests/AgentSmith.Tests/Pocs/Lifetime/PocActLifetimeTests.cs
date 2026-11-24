using AgentSmith.Application.Services.Pocs.Acts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Tests.Pocs.Acts;
using AgentSmith.Tests.Pocs.GitOps;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: c81ed's poc-stop and poc-delete started by the lifetime pass — the act stays stop or delete,
/// the run re-checks its case and records the audit act 'expired' (or 'deleted' for a removal) with its reason, or ends
/// green doing nothing; a start of an expired PoC is refused until it is extended.</summary>
public sealed class PocActLifetimeTests
{
    private readonly PocActRig _rig = new();

    private DateTimeOffset Now => _rig.Commit.Clock.Now;

    /// <summary>A row created 100 days ago on a 90-day target: expired ten days ago.</summary>
    private PocLifetimeFacts Expired(long? expiresAt = null, int createdDaysAgo = 100) => new(Now.AddDays(-createdDaysAgo), expiresAt, null, 0);

    private void Subject(string state, int replicas, int? revision, PocLifetimeFacts facts, long? expiredAt = null, bool uploadGone = false) =>
        _rig.Lifetimes.Subjects.Add(new PocLifetimeSubject(7, "shop", "main", state, replicas, revision, facts, expiredAt, uploadGone, "act-0"));

    [Fact]
    public async Task ExpiryStop_OfAPocAtZeroReplicas_CommitsNothing_AndRecordsExpiredNamingTheStop()
    {
        await _rig.DeployAsync();
        await _rig.RunAsync(PipelinePresets.PocStopName, runId: "owner-stop");
        _rig.Acts.State = PocStates.Stopped;
        Subject(PocStates.Stopped, 0, 2, Expired());
        var writes = _rig.Commit.Branch.Writes.Count;

        var (act, commit, pipeline) = await _rig.RunAsync(PipelinePresets.PocStopName, runId: "expiry-stop", lifetimeCase: PocLifetimeCases.ExpiryStop);

        act.IsSuccess.Should().BeTrue(act.Message);
        commit!.Message.Should().Contain("already holds revision 2");
        _rig.Commit.Branch.Writes.Should().HaveCount(writes, "the folder already holds 0 replicas");
        pipeline.Get<string>(ContextKeys.PocAct).Should().Be(PocActKinds.Stop);
        _rig.Commit.Acted.Last().Should().Match<PocActedEvent>(e => e.Act == PocActKinds.Stop && e.AuditAct == PocActs.Expired
            && e.Reason == "stopped, not deleted: no reminder was seen before expiry" && e.RunId == "expiry-stop");
    }

    [Fact]
    public async Task UnseenDelete_KeepsTheActDelete_AndNamesTheKeyInItsReason()
    {
        await _rig.DeployAsync();
        _rig.Acts.State = PocStates.Stopped;
        Subject(PocStates.Stopped, 0, 2, Expired(createdDaysAgo: 125), expiredAt: Now.AddDays(-31).UtcTicks);

        var (act, commit, pipeline) = await _rig.RunAsync(PipelinePresets.PocDeleteName, lifetimeCase: PocLifetimeCases.UnseenDelete);

        act.IsSuccess.Should().BeTrue(act.Message);
        commit!.IsSuccess.Should().BeTrue(commit.Message);
        (pipeline.Get<string>(ContextKeys.PocAct), pipeline.Get<bool>(ContextKeys.PocRolloutAbsent)).Should().Be((PocActKinds.Delete, true));
        _rig.Commit.Acted.Last().Should().Match<PocActedEvent>(e => e.Act == PocActKinds.Delete && e.AuditAct == PocActs.Expired
            && e.Reason!.Contains("30 days after its expiry stop") && e.Reason.Contains(PocLifetimeConfig.UnseenDeleteAfterDaysKey));
    }

    [Fact]
    public async Task Removal_OfAPocThatGotANewUploadMeanwhile_EndsGreenDoingNothing()
    {
        Subject(PocStates.Importing, 1, null, Expired(), uploadGone: true);

        var (act, commit, pipeline) = await _rig.RunAsync(PipelinePresets.PocDeleteName, lifetimeCase: PocLifetimeCases.Removal);

        act.IsSuccess.Should().BeTrue();
        act.Message.Should().Contain("no longer a blocked PoC without its upload").And.Contain("nothing is done");
        commit!.IsSuccess.Should().BeTrue();
        commit.Message.Should().StartWith("Nothing to commit");
        pipeline.TryGet<string>(ContextKeys.PocAct, out _).Should().BeFalse();
        (_rig.Commit.Branch.Writes.Count, _rig.Commit.Store.Absents).Should().Be((0, 0));
        _rig.Commit.Acted.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpiryStop_OfAPocExtendedMeanwhile_EndsGreenDoingNothing()
    {
        await _rig.DeployAsync();
        Subject(PocStates.Ready, 1, 2, Expired(expiresAt: Now.AddDays(80).UtcTicks));

        var (act, commit, _) = await _rig.RunAsync(PipelinePresets.PocStopName, lifetimeCase: PocLifetimeCases.ExpiryStop);

        act.Message.Should().Contain("is no longer expired");
        commit!.Message.Should().StartWith("Nothing to commit");
        _rig.Commit.Store.Poc.Replicas.Should().Be(1);
        _rig.Commit.Acted.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_OfAnExpiredStoppedPoc_IsRefusedNamingTheDateAndExtend_AfterAnExtensionItPasses()
    {
        await _rig.DeployAsync();
        await _rig.RunAsync(PipelinePresets.PocStopName, runId: "owner-stop");
        _rig.Acts.State = PocStates.Stopped;
        _rig.Acts.Lifetime = Expired();

        var (refused, _, _) = await _rig.RunAsync(PipelinePresets.PocStartName, runId: "start-1");
        _rig.Acts.Lifetime = Expired(expiresAt: Now.AddDays(90).UtcTicks);
        var (passed, _, _) = await _rig.RunAsync(PipelinePresets.PocStartName, runId: "start-2");

        refused.IsSuccess.Should().BeFalse();
        refused.Message.Should().Contain($"expired on {Now.AddDays(-10):yyyy-MM-dd}").And.Contain("extend it first");
        passed.IsSuccess.Should().BeTrue(passed.Message);
    }

    [Fact]
    public void Plan_AdmitsAnExpiryStopFromStopped_ButNotAnOwnersStop()
    {
        var plan = new PocActPlan();
        var stopped = PocActPlanTests.Row(PocStates.Stopped, 0);

        plan.WhyNot(PocActKinds.Stop, stopped, PocLifetimeCases.ExpiryStop).Should().BeNull();
        plan.WhyNot(PocActKinds.Stop, stopped).Should().Contain("stop needs it ready or failed");
        plan.WhyNot(PocActKinds.Start, stopped with { ExpiredOn = Now }).Should().Contain("extend it first");
    }
}
