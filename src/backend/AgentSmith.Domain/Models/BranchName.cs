namespace AgentSmith.Domain.Models;

/// <summary>
/// Git branch name. For ticket-based naming, use
/// <c>AgentSmith.Application.Services.TicketBranchNamer.Compose</c>.
/// </summary>
public sealed record BranchName
{
    public string Value { get; }

    public BranchName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
}
