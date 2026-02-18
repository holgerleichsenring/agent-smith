namespace AgentSmith.Domain.ValueObjects;

/// <summary>
/// Unique identifier for a ticket across any provider.
/// </summary>
public sealed record TicketId
{
    public string Value { get; }

    public TicketId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public static implicit operator string(TicketId id) => id.Value;
    public static implicit operator TicketId(string value) => new(value);
    public override string ToString() => Value;
}
