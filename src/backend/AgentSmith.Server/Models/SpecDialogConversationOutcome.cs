namespace AgentSmith.Server.Models;

/// <summary>
/// What a conversation filed, derived from the session's latest filing and latest proposal
/// rather than kept beside them — a second record of the same filing could disagree with it.
/// </summary>
/// <param name="Kind">"bug", "phase" or "epic" as the filing recorded it; a record written before
/// the filing kept its kind falls back to the latest proposal, and is null once that was discarded.</param>
/// <param name="Tickets">How many tickets the filing created.</param>
/// <param name="Partial">The filing stopped with an error after creating some of them.</param>
public sealed record SpecDialogConversationOutcome(string? Kind, int Tickets, bool Partial);
