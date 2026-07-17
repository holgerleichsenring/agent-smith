namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// Editable studio view of one secret. Only the env-NAME (<see cref="Id"/>) is
/// ever persisted or displayed — the value is resolved from the runtime
/// environment and the studio never sees it.
/// </summary>
public sealed record SecretEntity(string Id)
{
    public SecretEntity() : this(string.Empty) { }
}
