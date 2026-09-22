using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042eg: why a filed ticket started, or why it did not — in the words the operator
/// reads on the filed tab and in the thread. Each one names the thing that is in the way, because
/// "not started" on its own leaves a person guessing between a missing tag, a missing permission
/// and a tracker that refused.
/// <para>
/// THE RESOLVER DROPS A MATCH FOR THREE DIFFERENT REASONS and returns the same empty answer for
/// all of them: the resolution did not match, a blocking startup finding disabled the trigger, or
/// the project's pipeline rules matched no label. Telling an operator whose Jira credentials are
/// missing to add a tag their ticket already carries is a wrong answer, not a vague one, so the
/// cause is established from the data in hand before a sentence is chosen.
/// </para>
/// </summary>
internal static class FiledWorkReasons
{
    private const string Webhook =
        " The tracker's own webhook resolves more than the poller can, so it may still be claimed that way.";

    internal const string EveryStatusTriggers =
        "the project names no trigger statuses, so the status it was created in already triggers.";

    internal const string StatusUnreadable =
        "its status could not be read back, so nothing may claim it started. Check it by hand.";

    internal static bool Triggers(WebhookTriggerConfig trigger, string status) =>
        trigger.TriggerStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    /// <summary>GitHub adds a LABEL for any status but open/closed and GitLab throws on one, so
    /// on those two a move is attempted only for a state the tracker actually has.</summary>
    internal static bool IsNativeState(TrackerType tracker, string status) =>
        tracker is not (TrackerType.GitHub or TrackerType.GitLab)
        || status.ToLowerInvariant() is "open" or "opened" or "closed";

    internal static string NoTrigger(ResolvedProject project, string platform) =>
        $"nothing would route it: project '{project.Name}' declares no {platform} trigger at all, "
        + "so no poll of this tracker looks at it." + Webhook;

    /// <summary>The filing project did not match. <paramref name="blocking"/> is the findings
    /// list's own answer for this trigger, and <paramref name="matches"/> is what DID match.</summary>
    internal static string NotRouted(
        ResolvedProject project, string platform, WebhookTriggerConfig trigger,
        IncomingTicketEnvelope envelope, IReadOnlyList<ProjectMatch> matches, string? blocking)
    {
        var elsewhere = matches.Count == 0 ? string.Empty
            : $" It does resolve to {Named(matches)}, where a poll will spawn a run for it — but not "
              + "as the work this conversation filed.";
        return $"nothing would route it to '{project.Name}', which filed it: "
            + Cause(project, platform, trigger, envelope, blocking) + "." + elsewhere + Webhook;
    }

    private static string Cause(
        ResolvedProject project, string platform, WebhookTriggerConfig trigger,
        IncomingTicketEnvelope envelope, string? blocking)
    {
        if (blocking is not null)
            return $"the {platform} trigger of '{project.Name}' is disabled by a blocking startup "
                + $"finding — {blocking}. Fix that first; the tag on this ticket is not the problem";
        if (trigger.ProjectResolution is not { } resolution)
            return $"its {platform} trigger declares no project_resolution, so nothing resolves to it";
        // Only the tag strategy is re-read here, off the very labels the envelope carries. The
        // other three read an area path, a source repo or a to-address, and a POLLED envelope
        // carries labels, ticket id and platform only — so they are unsatisfiable by construction
        // rather than by a comparison this file would have to keep in step with the resolver.
        if (resolution.Strategy != ResolutionStrategy.Tag)
            return $"it resolves a {platform} ticket by {resolution.Strategy} '{resolution.Value}', "
                + "and a polled envelope carries labels, ticket id and platform only, so nothing "
                + "this filing creates can satisfy it";
        if (!envelope.Labels.Any(l => string.Equals(l, resolution.Value, StringComparison.OrdinalIgnoreCase)))
            return $"it resolves a {platform} ticket by Tag '{resolution.Value}', which this ticket "
                + "does not carry";
        return $"it carries the tag '{resolution.Value}' the project resolves by, so the project's "
            + $"own pipeline rules dropped it — no pipeline_from_label entry matched [{Labels(envelope)}]";
    }

    private static string Named(IReadOnlyList<ProjectMatch> matches) =>
        string.Join(", ", matches.Select(m => $"'{m.ProjectName}'").Distinct(StringComparer.Ordinal));

    private static string Labels(IncomingTicketEnvelope envelope) =>
        envelope.Labels.Count == 0 ? "no labels" : string.Join(", ", envelope.Labels);

    internal static string AlreadyTriggering(string status) =>
        $"it was created in '{status}', which triggers, so nothing was moved.";

    /// <summary>
    /// Nothing here is a WAIT: the permission is read once, on the request that opened the turn,
    /// and no later path re-reads it. So the sentence names the two things that do end it — a
    /// move made in the tracker, or a person who holds the permission making it there — and says
    /// what approving again would cost, which is a second work ticket and a second run.
    /// </summary>
    internal static string NoPermission(string status, string target) =>
        $"it sits in '{status}' and would have to move to '{target}' to start, which starts a run "
        + "and needs runs.control. Move it there in the tracker, or ask someone who holds "
        + "runs.control to move it — approving again files a SECOND work ticket and a second run.";

    internal static string NotNative(TrackerType tracker, string target) =>
        $"'{target}' is not a native {tracker} state, so moving it there would add a label or be "
        + "refused rather than trigger anything. Nobody can start it until the project's trigger "
        + "statuses name a state this tracker has.";

    internal static string MoveFailed(string target, string error) =>
        $"the move to '{target}' failed: {error}. Everything else was filed.";

    internal static string MoveHeld(string target, string status) =>
        $"it was moved to '{target}' and read back as '{status}', so the status held. A workflow "
        + "rule may be refusing the transition.";

    internal static string Moved(string status) => $"moved into the trigger status '{status}'.";
}
