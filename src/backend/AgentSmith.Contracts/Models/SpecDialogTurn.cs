namespace AgentSmith.Contracts.Models;

/// <summary>
/// One turn of a spec-dialog design conversation as it crosses the
/// server → pipeline boundary (p0315b). Role is "user" or "assistant".
/// </summary>
public sealed record SpecDialogTurn(string Role, string Text)
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";
}
