namespace AgentSmith.Server.Services.Webhooks;

/// <summary>2026-10-08-e8b9e: what a PR comment command is answered with, in our marked voice.</summary>
public static class PrCommandTexts
{
    public const string Marker = "<!-- agentsmith:command -->";

    public static string Started(string pipeline, string runId) => $"{Marker}\nAgent Smith — {pipeline} started as run `{runId}`.";

    public static string Queued(string pipeline, string runId, string reason) =>
        $"{Marker}\nAgent Smith — {pipeline} is queued as run `{runId}`: {reason}.";

    public static string WorkedOn(string ticketId, string? holder) => holder is null
        ? $"{Marker}\nAgent Smith — a run is starting on ticket {ticketId}; this command started nothing."
        : $"{Marker}\nAgent Smith — run `{holder}` is working on ticket {ticketId}; this command started nothing.";

    public static string NoHolder(string ticketId) =>
        $"{Marker}\nAgent Smith — ticket {ticketId} is taken by another trigger; this command started nothing.";

    public static string Refused(string reason) => $"{Marker}\nAgent Smith — this command started nothing: {reason}";
}
