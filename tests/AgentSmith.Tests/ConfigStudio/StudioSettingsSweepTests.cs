using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>2026-11-17-b34eb: the settings added on 2026-11-16/17 reach the studio and survive its saves.</summary>
public sealed class StudioSettingsSweepTests
{
    [Fact]
    public void Capabilities_ListTheModes_FromTheEnums()
    {
        var capabilities = ConfigStudioCapabilities.Build([]);

        capabilities.MergeWhen.Should().Equal([.. Enum.GetValues<MergeWhen>().Select(ConfigStudioCapabilities.SnakeName)]);
        capabilities.MergeWhen.Should().StartWith(["manual", "on_gate_green"]);
        capabilities.RequiresStart.Should().BeEquivalentTo(["on_merge", "on_green_push"]);
    }

    [Fact]
    public void ProjectMerge_RoundTripsThroughProjectionAndPatch()
    {
        var sent = new ProjectEntity("app", "a", "t", [], null, [],
            Merge: new ProjectMerge("on_gate_green", RespectBranchProtection: true, RefreshOpenPullRequests: true));

        var stored = RawProjectPatch.Apply(sent, new RawProjectEntry());

        stored.Merge!.When.Should().Be(MergeWhen.OnGateGreen);
        ProjectEntityMapping.ToProject("app", stored).Merge.Should().Be(sent.Merge with
        {
            CiPollSeconds = 60, CiSettleSeconds = 180, CiPendingSeconds = 3600, CiFixAttempts = 3, CiExcerptChars = 4000,
            CiFailedChecksRead = 5, // 2026-11-17-3435d: a block sent without the CI keys stores their defaults
        });
    }

    // 2026-11-17-3435d
    [Fact]
    public void ProjectMerge_CiLoopKeys_RoundTrip_AndASaveKeepsThem()
    {
        var sent = new ProjectEntity("app", "a", "t", [], null, [], Merge: new ProjectMerge("on_ci_green", true, false,
            CiPollSeconds: 30, CiSettleSeconds: 90, CiPendingSeconds: 600, CiFixAttempts: 2, CiExcerptChars: 1500,
            CiFailedChecksRead: 3, ApprovalAuth: "approver"));

        var stored = RawProjectPatch.Apply(sent, new RawProjectEntry());

        stored.Merge!.When.Should().Be(MergeWhen.OnCiGreen);
        (stored.Merge.CiFixAttempts, stored.Merge.ApprovalAuth).Should().Be((2, "approver"));
        ProjectEntityMapping.ToProject("app", stored).Merge.Should().Be(sent.Merge);
    }

    [Fact]
    public void ProjectMerge_AbsentInTheSave_KeepsTheStoredBlock_AndAProjectWithoutOneReadsNull()
    {
        var stored = new RawProjectEntry { Merge = new MergePolicyConfig { When = MergeWhen.OnGateGreen } };

        RawProjectPatch.Apply(new ProjectEntity("app", "a", "t", [], null, []), stored).Merge!.When
            .Should().Be(MergeWhen.OnGateGreen);
        ProjectEntityMapping.ToProject("app", new RawProjectEntry()).Merge.Should().BeNull();
    }

    [Fact]
    public void ProjectMerge_AnUnknownMode_IsRefusedByKey()
    {
        var sent = new ProjectEntity("app", "a", "t", [], null, [], Merge: new ProjectMerge("sometimes", false, false));

        var act = () => RawProjectPatch.Apply(sent, new RawProjectEntry());

        act.Should().Throw<ConfigurationException>().WithMessage("*merge.when 'sometimes'*manual*on_gate_green*");
    }

    [Fact]
    public void AgentCompactionRatios_AreWrittenWhenSent_AndKeptWhenNot()
    {
        var existing = new AgentConfig();
        existing.Compaction.FoldTargetRatio = 0.2;
        var entity = new AgentEntity() with
        {
            Id = "a", Provider = "openai",
            Compaction = new AgentCompactionSettings(true, 8, 200000, 3, SummaryBudgetRatio: 0.15),
        };

        var agent = RawConfigPatch.Agent(entity, existing);

        agent.Compaction.SummaryBudgetRatio.Should().Be(0.15);
        agent.Compaction.FoldTargetRatio.Should().Be(0.2, "a ratio the client did not send stays");
        agent.Compaction.MaxContextTokensTriggerRatio.Should().Be(0.7);
    }
}
