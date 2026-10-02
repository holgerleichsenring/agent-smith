using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-01-aeb6b: a ````document fence is a text the operator takes elsewhere. It is an
/// answer — never a draft, even when it carries a ```yaml example — and it is shown whole.
/// </summary>
public sealed class SpecDialogDocumentTests
{
    private const string Document =
        "````document\n# Hand-off\nThe spec looks like this:\n```yaml\nphase: p9999\ngoal: \"g\"\n```\nDone.\n````";

    private static OutcomeProposalResolver Resolver()
    {
        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var reader = new PhaseDraftReader();
        return new OutcomeProposalResolver(
            validator, reader, new BugOutcomeParser(),
            new EpicOutcomeParser(validator, reader, new RequiresEdgeChecker()));
    }

    [Fact]
    public void Resolve_ADocumentCarryingAYamlExample_IsAnAnswer()
    {
        var resolution = Resolver().Resolve("Here it is.\n\n" + Document);

        resolution.Should().BeOfType<OutcomeResolved>()
            .Which.Proposal.Should().BeOfType<AnswerOutcome>();
    }

    [Fact]
    public void Contains_ADraftInsideADocument_IsNoDraft() =>
        SpecDialogDraftBlocks.Contains("Here.\n" + Document).Should().BeFalse();

    [Fact]
    public void Strip_ADraftBesideADocument_RemovesTheDraftAndKeepsTheDocumentWhole()
    {
        var reply = "Before.\n\n```yaml\nphase: p1\n```\n\n" + Document + "\n\n\n\nAfter.";

        SpecDialogDraftBlocks.Strip(reply).Should().Be("Before.\n\n" + Document + "\n\nAfter.");
    }

    [Fact]
    public void SpecDialogDiscussionPrompt_FirstAnswer_AnswersADocumentRequestWithTheDocument()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(
            ContextKeys.SpecDialogTranscript, [new SpecDialogTurn("user", "give me a hand-off prompt")]);

        var prompt = new SpecDialogPromptFactory().Build(pipeline, 0, 0);

        prompt.Should().Contain("a request for a document — a prompt, a summary, a brief to take elsewhere — with the document itself");
    }
}
