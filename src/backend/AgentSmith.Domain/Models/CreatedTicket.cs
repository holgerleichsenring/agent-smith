namespace AgentSmith.Domain.Models;

/// <summary>
/// What a tracker returns for a newly created ticket: the provider-native id
/// and the human-facing web URL when the tracker exposes one.
/// <see cref="Reference"/> is the display form — the web URL where derivable,
/// else the provider-native ref (honest degrade, never a fake link).
/// </summary>
public sealed record CreatedTicket(TicketId Id, string? WebUrl)
{
    public string Reference => WebUrl ?? $"#{Id.Value}";

    /// <summary>
    /// The tracker's internal id where it differs from <see cref="Id"/> and a later call needs
    /// it: GitHub's database id, which a sub-issue link takes instead of the issue number.
    /// </summary>
    public string? NativeId { get; init; }
}
