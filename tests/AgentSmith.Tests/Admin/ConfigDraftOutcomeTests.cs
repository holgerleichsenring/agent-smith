using AgentSmith.Application.Services.Admin;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Admin;

/// <summary>2026-11-19-7c42a: the admin assistant's draft is discarded in words by dropping every change it continued.</summary>
public sealed class ConfigDraftOutcomeTests
{
    private static readonly ConfigChangeItem Change = new(ConfigChangeItem.Delete, "project", "alpha", null, null);

    [Fact]
    public void Of_ContinuedDraftEmptied_IsWithdrawnAnswer()
    {
        var draft = new AdminConfigDraft([Change]);
        draft.Drop("project", "alpha");

        Outcome(draft).Should().BeOfType<AnswerOutcome>().Which.Withdrawn.Should().BeTrue();
    }

    [Fact]
    public void Of_NoDraftContinued_IsPlainAnswer()
    {
        Outcome(new AdminConfigDraft()).Should().BeOfType<AnswerOutcome>().Which.Withdrawn.Should().BeFalse();
        Outcome(new AdminConfigDraft([Change])).Should().BeOfType<ConfigChangeOutcome>();
    }

    private static OutcomeProposal Outcome(AdminConfigDraft draft)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.AdminConfigDraft, draft);
        return new ConfigDraftOutcome().Of(pipeline);
    }
}
