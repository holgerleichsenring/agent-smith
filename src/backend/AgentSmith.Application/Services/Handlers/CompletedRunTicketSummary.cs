using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
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
/// 2026-11-17-7c9be: the Changes are what the branch changed against its base, read the way result.md
/// reads them (<see cref="RunResultChanges"/>), not the tool writes the last master pass published.
/// </summary>
public sealed class CompletedRunTicketSummary(RunResultChanges branchChanges)
{
    public async Task<string> BuildAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos, IReadOnlyList<OpenedPullRequest> opened,
        IReadOnlyList<CodeChange> toolWrites, RunShortfall? shortfall, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(toolWrites);
        var changes = (await branchChanges.ResolveAsync(pipeline, repos, toolWrites, ct)).Coverage;
        var changeLines = string.Join("\n", changes.Select(c => $"- [{c.ChangeType}] `{c.Path}`"));
        return $"""
            {Heading(repos.Count, shortfall)}

            ### Pull requests
            {RenderPullRequestList(opened)}

            ### Changes
            {changeLines}
            {RunAccountSection.Build(pipeline)}{ShortfallSection.Build(shortfall)}

            This ticket was automatically processed by Agent Smith.
            """;
    }

    private static string Heading(int repoCount, RunShortfall? shortfall) => shortfall is null
        ? $"## Agent Smith - Completed across {repoCount} repo(s)"
        : $"## Agent Smith - Delivered with a shortfall ({shortfall.Delivered.Count} of "
          + $"{shortfall.SpecCount} specs) across {repoCount} repo(s)";

    private static string RenderPullRequestList(IReadOnlyList<OpenedPullRequest> opened) =>
        string.Join("\n", opened.Select(o => o.Status switch
        {
            OpenStatus.Opened => $"- **{o.RepoName}**: {o.Url}",
            OpenStatus.SkippedNoChanges => $"- **{o.RepoName}**: _(no changes)_",
            _ => $"- **{o.RepoName}**: _(open failed)_",
        }));
}
