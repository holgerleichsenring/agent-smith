namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-e061d: the follow-up of a ticket a person closed, asked for by a person's act on it rather than by an
/// approved proposal — one work ticket carrying the act's text. It has no specs, so it gets no approved-set stamp: its
/// run derives from its text, like any ticket a person files.
/// </summary>
public sealed record FilingFollowUp(string Title, string Body);
