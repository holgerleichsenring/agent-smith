using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Rework.Overrule;

/// <summary>
/// 2026-10-13-8762p: an overruled default read off the previous attempt — an act while the attempt may not have
/// seen it, its own revision cause that re-derives an executed or approved set, its notice on the ticket, and the
/// section the derivation and the master read it from.
/// </summary>
public sealed class OverruleCauseTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly SpecApproval Prior = new(Start.AddDays(-1), "conv-7", "alice");

    private static DecidedDefault Overruled(int minutes, long id = 7, string? note = "use the bus") =>
        new(id, "run-1", "2026-10-13-1234a", "Which queue?", "polling", DefaultDecisionVerdicts.Overruled, note, "ana", Start.AddMinutes(minutes));

    private static PreviousAttempt Attempt(params DecidedDefault[] overrules) =>
        new("run-1", "success", Start, true) { ActsReadAt = Start.AddMinutes(10), Overrules = overrules };

    private static DerivedSpec Spec(string id) => new(
        new SpecDraft(id, $"Goal {id}", $"spec: {id}\ngoal: \"Goal {id}\"", []) { Done = [$"Done {id}."] }, id, string.Empty, []);

    private static SpecSet Executed(int specs, SpecApproval? approval = null, string cause = SpecRevisionCause.Initial)
    {
        var ids = Enumerable.Range(0, specs).Select(i => $"p{i}").ToList();
        return new SpecSet("k", [.. ids.Select(Spec)], SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, Start), new SpecRevision(2, cause, Start)],
            SpecSource.BranchArtifact, ExecutedSpecIds: ids) { Approval = approval };
    }

    private static Ticket Ticket() => new(new TicketId("1"), "t", "text", null, "open", "azdo", []);

    private static PipelineContext Pipeline(PreviousAttempt attempt)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PreviousAttempt, attempt);
        return pipeline;
    }

    [Fact]
    public void OverruleAct_OlderThanTheAttemptsRead_IsNoAct()
    {
        OverruleAct.Of(Attempt(Overruled(-10))).Should().BeNull("the attempt read its acts after this overrule");
        OverruleAct.TextOf(Attempt(Overruled(-10))).Should().BeNull();
        OverruleAct.Of(Attempt(Overruled(-10, id: 6), Overruled(30))).Should().Be(
            new ReworkAct("ana", Start.AddMinutes(30), ReworkChannel.Overrule) { Source = new AttemptSource(AttemptSourceKinds.Default, Id: "7") });
    }

    [Fact]
    public void SpecRevisionCause_OverruleAfterTheAttempt_IsOverrule_AndAmendsAnExecutedSet()
    {
        var cause = SpecRevisionCause.For(new SpecSetReadResult(Executed(2), "sha"), new SpecSetPointer("k", "primary", "sha", 1),
            Ticket(), Pipeline(Attempt(Overruled(30))));

        cause.Should().Be(SpecRevisionCause.Overrule);
        SpecAmendmentRule.NeedsModel(cause, Executed(2, Prior), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults).Should().BeTrue("an overrule re-derives an approved, executed set");
        SpecAmendmentRule.IsAppend(cause, Executed(2)).Should().BeTrue();
        SpecRevisionCause.For(new SpecSetReadResult(Executed(2), "sha"), new SpecSetPointer("k", "primary", "sha", 1),
            Ticket(), Pipeline(Attempt(Overruled(-10)))).Should().Be(SpecRevisionCause.Retrigger);
    }

    [Fact]
    public void SpecRecutNotice_Overrule_FullSet_SaysNothingWasAdded()
    {
        SpecRecutNotice.Render(Executed(AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults.MaxSpecs, cause: SpecRevisionCause.Overrule), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults)
            .Should().StartWith("A default was overruled on the ticket after").And.Contain("nothing was added");
        SpecRecutNotice.Render(Executed(2, cause: SpecRevisionCause.Overrule), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults).Should().Contain("with the overrule in view");
    }

    [Fact]
    public void PreviousCutPrompt_Overrule_AppendNamesTheOverruledDefaultsSection()
    {
        var rendered = PreviousCutPromptSection.Render(Executed(2), SpecRevisionCause.Overrule, AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Defaults);

        rendered.Should().Contain("A person overruled a default").And.Contain("ADD new specs after them that carry out what the overruled defaults section asks");
    }

    [Fact]
    public void SpecSourceResolver_Overrule_KeepsTheApproval_AndReDerives()
    {
        var decision = new SpecSourceResolver(new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance), ApprovedSetDoubles.Continuation(),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSourceResolver>.Instance)
            .Decide(SpecSetOnBranch.Answered(new SpecSetReadResult(Executed(2, Prior), "sha")), Ticket(),
                new SpecSetPointer("k", "primary", "sha", 1), Pipeline(Attempt(Overruled(30))), "k",
                AgentSmith.Contracts.Tickets.TicketLabelVocabulary.ForOptional(null));

        (decision.Cause, decision.NeedsModel, decision.Approval).Should().Be((SpecRevisionCause.Overrule, true, Prior));
    }

    [Fact]
    public void TicketConversationPrompt_RendersOverruledDefaultsWithTheirNotes()
    {
        var rendered = TicketConversationPromptSection.Render(Pipeline(Attempt(Overruled(-10, id: 5, note: "old"),
            Overruled(20, id: 6, note: null), Overruled(30))));

        rendered.Should().Contain(OverruledDefaultsPromptSection.Heading).And.Contain("Default #7 overruled by ana")
            .And.Contain("Question: Which queue?").And.Contain("Chosen by default: polling").And.Contain("Note: use the bus")
            .And.Contain("Default #6").And.Contain("Note: (none)").And.NotContain("Default #5");
    }
}
