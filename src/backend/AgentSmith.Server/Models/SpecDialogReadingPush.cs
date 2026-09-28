namespace AgentSmith.Server.Models;

/// <summary>
/// One repository a design turn opened, and how far that got — the payload of the hub's
/// "SpecDialogReading" push. <paramref name="State"/> is "opening", "ready" or "failed".
/// <para>
/// A plain payload into the dialog's group rather than a run event: a run event would also
/// fire into the trail of every coding run that opens a template, and the dialog page has
/// joined no run group to hear it.
/// </para>
/// </summary>
/// <param name="Kind">2026-09-27-481be: a repository or a TICKET. The line used to be found and
/// keyed by its name alone, so a ticket whose id matched a repository name would have overwritten
/// that repository's line — and the sentence above the lines said "opening the repositories it
/// needs" whatever was being read.</param>
public sealed record SpecDialogReadingPush(
    string DialogId, string Kind, string Name, string State, DateTimeOffset At);

/// <summary>What a reading line is about. The dashboard words each in its own way.</summary>
public static class SpecDialogReadingKinds
{
    public const string Repository = "repository";

    public const string Ticket = "ticket";
}
