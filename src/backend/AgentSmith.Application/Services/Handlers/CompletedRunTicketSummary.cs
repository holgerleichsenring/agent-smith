using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The comment a COMPLETED run leaves on its ticket: the pull requests, the changes and
/// the account the run was judged by. Its counterpart on a failed run is the error
/// path's failure comment (FailureTicketComment); this text is never posted there.
/// p0439: a run that delivered with a shortfall is a completed run that says so — the
/// heading names the shortfall and the "Not delivered" section names what it lacks.
/// </summary>
public sealed class CompletedRunTicketSummary
{
    public string Build(
        PipelineContext pipeline, int repoCount, IReadOnlyList<OpenedPullRequest> opened,
        IReadOnlyList<CodeChange> changes, RunShortfall? shortfall = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(changes);
        var changeLines = string.Join("\n", changes.Select(c => $"- [{c.ChangeType}] `{c.Path}`"));
        return $"""
            {Heading(repoCount, shortfall)}

            ### Pull requests
            {RenderPullRequestList(opened)}

            ### Changes
            {changeLines}
            {RunAccountSection.Build(pipeline)}{DeclinedCriteriaSection.Build(pipeline)}{ShortfallSection.Build(shortfall)}

            This ticket was automatically processed by Agent Smith.
            """;
    }

    private static string Heading(int repoCount, RunShortfall? shortfall) => shortfall is null
        ? $"## Agent Smith - Completed across {repoCount} repo(s)"
        : $"## Agent Smith - Delivered with a shortfall ({shortfall.Delivered.Count} of "
          + $"{shortfall.PhaseCount} phases) across {repoCount} repo(s)";

    private static string RenderPullRequestList(IReadOnlyList<OpenedPullRequest> opened) =>
        string.Join("\n", opened.Select(o => o.Status switch
        {
            OpenStatus.Opened => $"- **{o.RepoName}**: {o.Url}",
            OpenStatus.SkippedNoChanges => $"- **{o.RepoName}**: _(no changes)_",
            _ => $"- **{o.RepoName}**: _(open failed)_",
        }));
}
