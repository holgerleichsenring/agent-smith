using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-20-2ba8: puts the project's own ROUTING TAG on the ticket the filing just created, so
/// the resolution <see cref="FiledWorkStarter"/> is about to run names the project that filed it.
/// The filers stamp framework labels only — the phase label and the approved-set stamp — and of
/// the four resolution strategies only Tag can be satisfied by a polled envelope at all.
/// <para>
/// IT RUNS HERE, NOT AT CREATION. The work ticket is created, its approved set is stored, its
/// records are filed and only THEN is it started, because a ticket that becomes claimable before
/// its set exists is claimed by the poller and the run derives its own spec. An untagged ticket
/// is unresolvable and therefore harmless in that window; a tagged one is not. So the tag goes
/// where the start already goes. A slice record never reaches the starter, so it is never tagged
/// — by construction rather than by a carve-out anyone could later forget.
/// </para>
/// <para>
/// THE VALUE IS OPERATOR TEXT GOING ONTO SOMEONE ELSE'S TRACKER, so it is guarded before it is
/// sent: never when empty, and never when the tracker's own label grammar cannot carry it. A
/// value that is refused is SKIPPED and reported, never sent — a call the tracker rejects would
/// cost the filing itself, turning "filed but not started" into "nothing was filed".
/// </para>
/// </summary>
public sealed class FiledWorkTagger(ILogger<FiledWorkTagger> logger)
{
    /// <summary>Never throws: a tracker that refuses the tag is a sentence on the report, and the
    /// rest of the filing carries on with the labels the ticket actually has.</summary>
    public async Task<FiledWorkTagging> ApplyAsync(
        ITicketProvider provider, ResolvedProject project, CreatedTicket ticket,
        IReadOnlyList<string> labels, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(ticket);
        var platform = project.Tracker.Type.ToString().ToLowerInvariant();
        var trigger = TriggerSelectionHelper.ByKind(project, platform);
        // Anything but Tag reads an area path, a source repo or a to-address, and no label can
        // put one of those on a polled envelope. The starter's reason already says exactly that.
        if (trigger?.ProjectResolution is not { Strategy: ResolutionStrategy.Tag } resolution)
            return FiledWorkTagging.Silent(labels);
        var tag = resolution.Value;
        if (string.IsNullOrWhiteSpace(tag)) return new FiledWorkTagging(labels, Empty(platform));
        // Already carried is the ordinary case where the operator resolves by the phase label:
        // the comparison is the resolver's own, so a case variant is not written a second time.
        if (labels.Contains(tag, StringComparer.OrdinalIgnoreCase))
            return FiledWorkTagging.Silent(labels);
        return Refused(project.Tracker.Type, tag) is { } refusal
            ? new FiledWorkTagging(labels, refusal)
            : await WriteAsync(provider, project.Tracker.Type, ticket, labels, tag, ct);
    }

    private async Task<FiledWorkTagging> WriteAsync(
        ITicketProvider provider, TrackerType tracker, CreatedTicket ticket,
        IReadOnlyList<string> labels, string tag, CancellationToken ct)
    {
        try
        {
            // The label joins the envelope ONLY where the write landed. A tag the tracker did not
            // take would resolve here and nowhere else, and the ticket would be moved into a
            // trigger status it sits in forever, reported as started.
            if (!await provider.AddLabelAsync(ticket.Id, tag, ct))
                return new FiledWorkTagging(labels, Unsupported(tracker, tag));
            return new FiledWorkTagging([.. labels, tag], Added(tag));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Tagging filed ticket {Ticket} with '{Tag}' threw", ticket.Id.Value, tag);
            return new FiledWorkTagging(labels, Failed(tag, ex.Message));
        }
    }

    /// <summary>
    /// What each tracker's label grammar cannot carry, named so the operator can fix the value.
    /// GitLab joins labels on a comma and Azure DevOps on a semicolon, so a value containing the
    /// delimiter would arrive as two labels and resolve to nothing; Jira refuses a label with
    /// whitespace in it outright, and the refusal would be an HTTP error, not a silent split.
    /// </summary>
    internal static string? Refused(TrackerType tracker, string tag) => tracker switch
    {
        TrackerType.GitLab when tag.Contains(',') => Refusal(tracker, tag, "a comma"),
        TrackerType.AzureDevOps when tag.Contains(';') => Refusal(tracker, tag, "a semicolon"),
        TrackerType.Jira when tag.Any(char.IsWhiteSpace) => Refusal(tracker, tag, "whitespace"),
        _ => null,
    };

    private static string Added(string tag) => $"the tag '{tag}' was added to it; ";

    private static string Empty(string platform) =>
        $"no tag was written: the {platform} trigger resolves by Tag and its value is empty, so "
        + "there is nothing to put on the ticket — set project_resolution.value; ";

    private static string Refusal(TrackerType tracker, string tag, string what) =>
        $"the tag '{tag}' was NOT written: a {tracker} label cannot carry {what}, and sending it "
        + "would cost the filing rather than start it. Change the value the project resolves by; ";

    private static string Unsupported(TrackerType tracker, string tag) =>
        $"the tag '{tag}' was not added: this build cannot put a label on a {tracker} ticket that "
        + "already exists, so it has to be added by hand; ";

    private static string Failed(string tag, string error) =>
        $"adding the tag '{tag}' failed and everything else was filed anyway: {error}; ";
}
