using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ec: a design turn may propose work only once the conversation has held a
/// discussion — an assistant turn recorded as an answer, followed anywhere later by a message
/// from the operator. The whole transcript is read, not its latest turn: an edit note re-runs
/// the turn over a transcript that ends in a proposal and the note, and that turn must propose
/// again.
/// <para>
/// The transcript is the one the turn began with, so an ask_human answer given during the turn
/// unlocks nothing. A turn kept before kinds were recorded counts as an answer: such a
/// conversation was already held.
/// </para>
/// </summary>
public static class SpecDialogProposalRule
{
    public static bool MayPropose(IReadOnlyList<SpecDialogTurn> transcript)
    {
        var answered = false;
        foreach (var turn in transcript)
        {
            if (answered && turn.Role == SpecDialogTurn.UserRole) return true;
            if (turn.Role == SpecDialogTurn.AssistantRole && turn.Kind is null or SpecDialogTurnKind.Answer)
                answered = true;
        }
        return false;
    }

    public static bool MayPropose(PipelineContext pipeline) =>
        MayPropose(pipeline.TryGet<IReadOnlyList<SpecDialogTurn>>(
            ContextKeys.SpecDialogTranscript, out var transcript) && transcript is not null ? transcript : []);
}
