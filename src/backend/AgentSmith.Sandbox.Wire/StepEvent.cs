namespace AgentSmith.Sandbox.Wire;

public sealed record StepEvent(
    int SchemaVersion,
    Guid StepId,
    StepEventKind Kind,
    string Line,
    DateTimeOffset Timestamp)
{
    /// <summary>Stated once in <see cref="WireProtocol"/>.</summary>
    public const int CurrentSchemaVersion = WireProtocol.Current;
}
