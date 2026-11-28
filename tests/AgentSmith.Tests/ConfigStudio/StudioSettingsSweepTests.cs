using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
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

        capabilities.Review.Should().Equal(["end", "each", "none"]); // 2026-11-25-9992i
        capabilities.RequiresStart.Should().BeEquivalentTo(["on_merge", "on_green_push"]);
    }

    // 2026-11-17-3435d: a block sent without the CI keys stores their defaults.
    [Fact]
    public void ProjectMerge_RoundTripsThroughProjectionAndPatch()
    {
        var sent = new ProjectEntity("app", "a", "t", [], null, [], Merge: new ProjectMerge());

        var stored = RawProjectPatch.Apply(sent, new RawProjectEntry());

        ProjectEntityMapping.ToProject("app", stored).Merge.Should().Be(new ProjectMerge(
            CiPollSeconds: 60, CiSettleSeconds: 180, CiPendingSeconds: 3600, CiFixAttempts: 3, CiExcerptChars: 4000, CiFailedChecksRead: 5));
    }

    // 2026-11-17-3435d
    [Fact]
    public void ProjectMerge_CiLoopKeys_RoundTrip_AndASaveKeepsThem()
    {
        var sent = new ProjectEntity("app", "a", "t", [], null, [], Merge: new ProjectMerge(
            CiPollSeconds: 30, CiSettleSeconds: 90, CiPendingSeconds: 600, CiFixAttempts: 2, CiExcerptChars: 1500,
            CiFailedChecksRead: 3));

        var stored = RawProjectPatch.Apply(sent, new RawProjectEntry());

        stored.Merge!.CiFixAttempts.Should().Be(2);
        ProjectEntityMapping.ToProject("app", stored).Merge.Should().Be(sent.Merge);
    }

    [Fact]
    public void ProjectMerge_AbsentInTheSave_KeepsTheStoredBlock_AndAProjectWithoutOneReadsNull()
    {
        var stored = new RawProjectEntry { Merge = new MergePolicyConfig { CiPollSeconds = 15 } };

        RawProjectPatch.Apply(new ProjectEntity("app", "a", "t", [], null, []), stored).Merge!.CiPollSeconds.Should().Be(15);
        ProjectEntityMapping.ToProject("app", new RawProjectEntry()).Merge.Should().BeNull();
    }

    // 2026-11-25-9992i: review crosses as written, reads as end when the project says nothing, and survives a save without it.
    [Fact]
    public void Review_RoundTrips_DefaultsToEnd_AndASaveWithoutItKeepsIt()
    {
        var stored = RawProjectPatch.Apply(new ProjectEntity("app", "a", "t", [], null, [], Review: "none"), new RawProjectEntry());

        stored.Review.Should().Be("none");
        ProjectEntityMapping.ToProject("app", stored).Review.Should().Be("none");
        RawProjectPatch.Apply(new ProjectEntity("app", "a", "t", [], null, []), stored).Review.Should().Be("none");
        RawProjectPatch.Apply(new ProjectEntity("app", "a", "t", [], null, [], Review: " "), stored).Review.Should().BeNull();
        ProjectEntityMapping.ToProject("app", new RawProjectEntry()).Review.Should().Be("end");
    }

    // 2026-11-25-9992i: an unknown review is refused at save, by value and with the allowed names, as merge.when's was.
    [Fact]
    public void Review_AnUnknownValue_IsRefusedAtSave()
    {
        var act = () => RawProjectPatch.Apply(new ProjectEntity("app", "a", "t", [], null, [], Review: "on_ci_green"), new RawProjectEntry());

        act.Should().Throw<AgentSmith.Domain.Exceptions.ConfigurationException>()
            .WithMessage("*review 'on_ci_green' is not a known mode (end, each, none)*");
    }

    [Fact]
    public void AgentCompactionRatios_AreWrittenWhenSent_AndKeptWhenNot()
    {
        var existing = new AgentConfig();
        existing.Compaction.FoldTargetRatio = 0.2;
        var entity = new AgentEntity() with
        {
            Id = "a", Provider = "openai",
            Compaction = new AgentCompactionSettings(true, 200000, 3, SummaryBudgetRatio: 0.15),
        };

        var agent = RawConfigPatch.Agent(entity, existing);

        agent.Compaction.SummaryBudgetRatio.Should().Be(0.15);
        agent.Compaction.FoldTargetRatio.Should().Be(0.2, "a ratio the client did not send stays");
        agent.Compaction.MaxContextTokensTriggerRatio.Should().Be(0.7);
    }
}
