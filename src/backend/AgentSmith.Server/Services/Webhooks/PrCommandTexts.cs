using AgentSmith.Application.Models;

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

    /// <summary>2026-10-13-0b82b: the branch lacks the bootstrap files the run would refuse over.</summary>
    public static string NotBootstrapped(string branch, IReadOnlyList<MissingBootstrapFile> missing) =>
        $"{Marker}\nAgent Smith — this command started nothing: branch '{branch}' is not initialised. "
        + string.Join("; ", missing.Select(m => m.Describe()))
        + ". Run init-project for this project, merge its pull request and bring this branch up to date, then ask again.";

    public static string Refused(string reason) => $"{Marker}\nAgent Smith — this command started nothing: {reason}";
}
