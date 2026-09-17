using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// One ordered turn in a spec-dialog transcript. 2026-09-17-042ec: an assistant turn records
/// what it produced; a stored row without a kind reads as null. 2026-09-17-042el: an operator turn
/// that answered an approval question records its decision; a row without one reads as null.
/// </summary>
public sealed record TranscriptTurn(
    TranscriptRole Role, string Text, DateTimeOffset At, SpecDialogTurnKind? Kind = null,
    SpecDialogDecision? Decision = null);
