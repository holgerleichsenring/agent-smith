using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The stored transcript as the dashboard shows it. The store keeps every reply whole — it is
/// the design master's memory — so the draft is taken out here, where the reply is shown, and
/// only from what the assistant said: an operator who pastes a draft reads it back as written.
/// </summary>
internal static class SpecDialogShownTranscript
{
    internal static IReadOnlyList<SpecDialogTurnView> Turns(IReadOnlyList<TranscriptTurn> transcript) =>
        [.. transcript.Select(turn => new SpecDialogTurnView(
            turn.Role.ToString().ToLowerInvariant(),
            turn.Role == TranscriptRole.Assistant ? SpecDialogDraftBlocks.Strip(turn.Text) : turn.Text,
            turn.At))];

    /// <summary>
    /// The last assistant turn that carried a draft. No index travels with the proposal: live,
    /// its push follows the reply it came from, and the stored transcript is whole, so the
    /// turn that carried the latest draft is found where it was said.
    /// </summary>
    internal static int? CardTurn(IReadOnlyList<TranscriptTurn> transcript)
    {
        for (var index = transcript.Count - 1; index >= 0; index--)
            if (transcript[index].Role == TranscriptRole.Assistant
                && SpecDialogDraftBlocks.Contains(transcript[index].Text))
                return index;
        return null;
    }
}
