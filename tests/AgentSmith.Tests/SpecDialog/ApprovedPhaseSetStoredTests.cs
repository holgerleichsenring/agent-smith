using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-0e79a: approving a phase stores the reviewed draft under the spec key of the ticket
/// it files, with the approval instant, the conversation and the principal it came from, and the
/// scope's repositories. Storing is part of FILING: the ticket body no longer carries the spec.
/// </summary>
public sealed class ApprovedPhaseSetStoredTests
{
    [Fact]
    public async Task ApprovedPhase_Filed_IsStoredUnderTheTicketsSpecKey()
    {
        var store = ApprovedSetDoubles.Store();

        await ApprovedSetDoubles.Recorder(store).RecordAsync(
            State(), Project(), "19106", [Draft("p19106a")], default);

        var record = await store.GetAsync("sample-tracker", SpecSetKey.For("azuredevops", "19106").Value, default);
        record.Should().NotBeNull("the run computes exactly this key from the tracker type and the ticket id");
        record!.Set.Source.Should().Be(SpecSource.Approved);
        record.Set.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p19106a");
        record.Approval!.Conversation.Should().Be("session-1");
        record.Approval.Principal.Should().Be("sample.user");
        record.Repositories.Should().Equal(["sample-api"], "the scope's repositories are the approval's");
        record.Tracker.Should().Be("sample-tracker",
            "the spec key carries the tracker TYPE; the instance is the other half of the identity");
    }

    /// <summary>
    /// 2026-09-25-c1f7: the record also stores the TRACKER'S OWN ticket id, because discovery has
    /// to name it in a JQL or WIQL clause and the spec key it is stored under has already lowered
    /// it and replaced every non-alphanumeric character.
    /// </summary>
    [Fact]
    public async Task ApprovedPhase_Stored_CarriesTheTrackersOwnTicketId()
    {
        var store = ApprovedSetDoubles.Store();

        await ApprovedSetDoubles.Recorder(store).RecordAsync(
            State(), Project(), "DPG-1239", [Draft("p19106a")], default);

        var key = SpecSetKey.For("azuredevops", "DPG-1239");
        key.Value.Should().Be("azuredevops-dpg-1239", "the key cannot give the id back");
        (await store.GetAsync("sample-tracker", key.Value, default))!
            .TicketId.Should().Be("DPG-1239");
    }

    /// <summary>
    /// Numbering and cause are the RUN's bookkeeping, written when it publishes the set to the
    /// ticket branch — a stored revision would make the first published revision the second.
    /// </summary>
    [Fact]
    public async Task ApprovedPhase_Stored_CarriesNoRevisionYet()
    {
        var store = ApprovedSetDoubles.Store();

        await ApprovedSetDoubles.Recorder(store).RecordAsync(
            State(), Project(), "19106", [Draft("p19106a")], default);

        (await store.GetAsync("sample-tracker", SpecSetKey.For("azuredevops", "19106").Value, default))!
            .Set.Revisions.Should().BeEmpty();
    }

    [Fact]
    public async Task ApprovedPhase_ASessionWithNoScopedRepos_FallsBackToTheProjectsOwn()
    {
        var store = ApprovedSetDoubles.Store();

        await ApprovedSetDoubles.Recorder(store).RecordAsync(
            State() with { Scope = null }, Project(), "19106", [Draft("p19106a")], default);

        (await store.GetAsync("sample-tracker", SpecSetKey.For("azuredevops", "19106").Value, default))!
            .Repositories.Should().Equal("sample-api", "sample-web");
    }

    /// <summary>
    /// The cap is checked on the PROPOSAL, before any ticket exists: the filers create and then
    /// store, so a refusal at store time would leave a filed ticket with no set.
    /// </summary>
    [Fact]
    public void Proposal_OverMaxPhases_IsRefusedBeforeAnythingIsFiled()
    {
        var parser = new EpicOutcomeParser(
            new SpecDraftValidator(new PhaseSpecSchemaProvider()),
            new PhaseDraftReader(),
            new RequiresEdgeChecker());

        var resolution = parser.Parse(OutcomeYamlReader.ReadMap(EpicYaml(SpecSet.MaxPhases + 1)));

        resolution.Should().BeOfType<OutcomeInvalid>()
            .Which.Error.Should().Contain(SpecSet.MaxPhases.ToString());
    }

    [Fact]
    public void Proposal_AtTheCap_IsStillAccepted()
    {
        var parser = new EpicOutcomeParser(
            new SpecDraftValidator(new PhaseSpecSchemaProvider()),
            new PhaseDraftReader(),
            new RequiresEdgeChecker());

        parser.Parse(OutcomeYamlReader.ReadMap(EpicYaml(SpecSet.MaxPhases)))
            .Should().BeOfType<OutcomeResolved>();
    }

    private static string EpicYaml(int children)
    {
        var slices = string.Join("\n", Enumerable.Range(0, children).Select(i => $"""
          - phase: p910{i}
            goal: "Slice {i} of the sample migration"
            steps:
              - id: s{i}
                action: "Do slice {i}"
            done:
              - "Slice {i} is finished."
        """));
        return $"""
        kind: epic
        parent:
          phase: p9000
          goal: "The sample migration"
          steps:
            - id: all
              action: "Migrate the sample estate"
          done:
            - "The estate is migrated."
        children:
        {slices}
        """;
    }

    private static PhaseDraft Draft(string id) =>
        new(id, "Do the thing", $"phase: {id}\ngoal: \"Do the thing\"", []) { Done = ["It is done."] };

    private static ConversationState State() => new()
    {
        JobId = "session-1",
        ChannelId = "channel",
        UserId = "sample.user",
        Platform = "dashboard",
        Project = "sample",
        TicketId = string.Empty,
        StartedAt = ApprovedSets.Noon,
        Scope = new ActiveScope { Project = "sample", Repos = ["sample-api"] },
    };

    private static ResolvedProject Project() => new()
    {
        Name = "sample",
        Tracker = new TrackerConnection { Name = "sample-tracker", Type = TrackerType.AzureDevOps },
        Repos = [new RepoConnection { Name = "sample-api" }, new RepoConnection { Name = "sample-web" }],
    };
}
