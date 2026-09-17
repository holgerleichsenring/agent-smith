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
public sealed record SpecDialogReadingPush(string DialogId, string Repo, string State, DateTimeOffset At);
