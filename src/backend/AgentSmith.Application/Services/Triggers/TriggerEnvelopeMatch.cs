using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Application.Services.Triggers;

/// <summary>
/// 2026-09-25-8e51a: whether one project trigger matches one incoming envelope — the PREDICATE,
/// with none of the decisions that surround it.
/// <para>
/// Extracted because a design conversation needs the same matching and none of the same
/// decisions. The run-side resolver refuses a record-labelled ticket before every other rule,
/// skips a trigger carrying a blocking startup finding, drops a ticket whose labels satisfy no
/// pipeline-from-label entry, and emits an ambiguity metric — every one of them right for a RUN
/// and wrong for a conversation, where a person may discuss any ticket on the board, including
/// one nothing would route and one whose poller is disabled. So the predicate is shared and the
/// four decisions stay exactly where they are.
/// </para>
/// </summary>
public static class TriggerEnvelopeMatch
{
    /// <summary>The per-platform triggers a project declares, with the platform each belongs to.</summary>
    public static IEnumerable<(string Kind, WebhookTriggerConfig Trigger)> Triggers(ResolvedProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.GithubTrigger is not null) yield return ("github", project.GithubTrigger);
        if (project.GitlabTrigger is not null) yield return ("gitlab", project.GitlabTrigger);
        if (project.AzuredevopsTrigger is not null) yield return ("azuredevops", project.AzuredevopsTrigger);
        if (project.JiraTrigger is not null) yield return ("jira", project.JiraTrigger);
    }

    public static bool Matches(
        WebhookTriggerConfig trigger, ResolvedProject project, IncomingTicketEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(envelope);
        var resolution = trigger.ProjectResolution;
        if (resolution is null) return false;

        return resolution.Strategy switch
        {
            ResolutionStrategy.Tag => MatchesTag(envelope, resolution.Value),
            ResolutionStrategy.AreaPath => AreaPathNormalizer.IsPrefix(resolution.Value, envelope.AreaPath),
            ResolutionStrategy.Repo => MatchesRepo(envelope, project),
            ResolutionStrategy.ToAddress => MatchesToAddress(envelope, resolution.Value),
            _ => false,
        };
    }

    /// <summary>
    /// 2026-09-25-8e51a: true for a strategy a ticket READ BY ID can never satisfy. An envelope
    /// built from a fetched ticket carries labels, id and platform — a ticket has no area path and
    /// no source repository. Such a project is not "no match"; it is UNANSWERABLE from a ticket,
    /// and telling the two apart is the difference between a reason and an accusation.
    /// </summary>
    public static bool UnanswerableFromATicket(WebhookTriggerConfig trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger.ProjectResolution?.Strategy
            is ResolutionStrategy.AreaPath or ResolutionStrategy.Repo or ResolutionStrategy.ToAddress;
    }

    private static bool MatchesTag(IncomingTicketEnvelope envelope, string value)
        => envelope.Labels.Any(l => string.Equals(l, value, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesRepo(IncomingTicketEnvelope envelope, ResolvedProject project)
    {
        if (string.IsNullOrEmpty(envelope.SourceRepoUrl)) return false;
        if (project.Repos.Count != 1) return false;
        var url = project.Repos[0].Url;
        return !string.IsNullOrEmpty(url)
            && string.Equals(url, envelope.SourceRepoUrl, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesToAddress(IncomingTicketEnvelope envelope, string value)
        => !string.IsNullOrEmpty(envelope.ToAddress)
            && string.Equals(envelope.ToAddress, value, StringComparison.OrdinalIgnoreCase);
}
