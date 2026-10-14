namespace AgentSmith.Contracts.Models;

/// <summary>
/// One turn of a spec-dialog design conversation as it crosses the
/// server → pipeline boundary (p0315b). Role is "user" or "assistant".
/// 2026-09-17-042ec: <paramref name="Kind"/> is what an assistant turn produced, null for an
/// operator message and for a turn kept before kinds were recorded.
/// <paramref name="Author"/> is the display name of the person who wrote a user turn, null when
/// none was recorded. <paramref name="DigestThrough"/> is, on a digest turn, the index of the last turn it folds.
/// </summary>
public sealed record SpecDialogTurn(
    string Role, string Text, SpecDialogTurnKind? Kind = null, string? Author = null, int? DigestThrough = null)
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";
}
