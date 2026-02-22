namespace AgentSmith.Domain.Models;

/// <summary>
/// Git branch name, with factory method for ticket-based naming.
/// </summary>
public sealed record BranchName
{
    public string Value { get; }

    public BranchName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public static BranchName FromTicket(TicketId ticketId, string prefix = "fix")
    {
        return new BranchName($"{prefix}/{ticketId.Value}");
    }

    public override string ToString() => Value;
}
