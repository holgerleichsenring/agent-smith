namespace AgentSmith.Sandbox.Wire;

public sealed record StepEvent(
    int SchemaVersion,
    Guid StepId,
    StepEventKind Kind,
    string Line,
    DateTimeOffset Timestamp)
{
    public const int CurrentSchemaVersion = 1;
}
