namespace AgentSmith.Contracts.Models;

/// <summary>
/// One turn of a spec-dialog design conversation as it crosses the
/// server → pipeline boundary (p0315b). Role is "user" or "assistant".
/// 2026-09-17-042ec: <paramref name="Kind"/> is what an assistant turn produced, null for an
/// operator message and for a turn kept before kinds were recorded.
/// </summary>
public sealed record SpecDialogTurn(string Role, string Text, SpecDialogTurnKind? Kind = null)
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";
}
