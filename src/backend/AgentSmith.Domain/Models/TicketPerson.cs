namespace AgentSmith.Domain.Models;

/// <summary>
/// A person a ticket names, in the provider's own terms: what to show a reader
/// (<paramref name="DisplayName"/>) and what the provider needs to resolve the
/// mention (<paramref name="ProviderId"/> — an Azure DevOps identity GUID, a Jira
/// account id, a GitHub login, a GitLab username).
/// <para>
/// Both halves are required. A display name without an id renders a mention the
/// platform will not deliver, which reads as a notification and is not one.
/// </para>
/// </summary>
public sealed record TicketPerson(string DisplayName, string ProviderId)
{
    /// <summary>
    /// The person, or null when the provider gave only half an identity — so the
    /// mention falls through to the next candidate instead of naming someone
    /// unreachable.
    /// </summary>
    public static TicketPerson? From(string? displayName, string? providerId) =>
        string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(providerId)
            ? null
            : new TicketPerson(displayName.Trim(), providerId.Trim());
}
