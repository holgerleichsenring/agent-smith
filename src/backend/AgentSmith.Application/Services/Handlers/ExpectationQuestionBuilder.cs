using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Expectations;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0328: builds the ratification DialogQuestion for a drafted expectation.
/// Type=Approval so the existing typed transports (Slack approval blocks,
/// Teams approval card, dashboard PendingQuestionCard) render approve/reject
/// affordances; an edit rides as the answer/comment text and is parsed back
/// into the schema. The timeout default is "approve" — but a system-answered
/// default ratifies as 'unratified' (visible degradation), never as consent;
/// the code-change gate stays with the later Approval step, whose silence
/// still rejects.
/// </summary>
public sealed class ExpectationQuestionBuilder
{
    private const int DefaultRatificationTimeoutSeconds = 259_200; // 3 days

    public DialogQuestion Build(ExpectationDraft draft, PipelineContext pipeline) => new(
        QuestionId: Guid.NewGuid().ToString("N"),
        Type: QuestionType.Approval,
        Text: "Ratify this expectation? It becomes the run's acceptance contract. "
              + "Reply 'approve', 'reject', or an edited version of the block below.",
        Context: ExpectationMarkdown.Render(draft),
        Choices: null,
        DefaultAnswer: "approve",
        Timeout: TimeSpan.FromSeconds(ResolveTimeoutSeconds(pipeline)));

    // Rides the p0327 approval-timeout config — ratification is the same
    // days-scale human wait class; a separate knob would add nothing.
    private static int ResolveTimeoutSeconds(PipelineContext pipeline) =>
        pipeline.TryGet<int>(ContextKeys.DialogueApprovalTimeoutSeconds, out var seconds)
        && seconds > 0 ? seconds : DefaultRatificationTimeoutSeconds;
}
