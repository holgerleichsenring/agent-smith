using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-09-0620c: the user prompt of a pull-request review. It used to be the security-scan prompt, because the
/// review master declares the same output schema: told it ran a security review, that nobody had handed it suspects,
/// and to inventory the surface itself — against a skill that judges the diff it was shown.
/// </summary>
internal static class PrReviewPrompt
{
    internal static string Build(Repository repository, string repos, string conversation) =>
        $"""
        ## The checkout
        The repository is checked out at the pull request's head. Your tools read it; nothing you do changes it.
        **Path:** {repository.LocalPath}
        **Branch:** {repository.CurrentBranch}
        {repos}
        {conversation}
        Judge the change the diff above shows. Where a judgement depends on code the diff does not show, read that code.
        The framework parses your last message as the JSON observation array your methodology describes — `[]` when
        nothing survives your own refutation.
        """;
}
