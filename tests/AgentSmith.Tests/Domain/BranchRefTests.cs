using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Domain;

/// <summary>
/// 2026-10-26-cb25e: the six refs/heads/ strippers' matrix, run against their one home. "was" names what the copies
/// answered — leading = TicketBranchNamer, AzureReposPullRequestTarget, AzureDevOpsPrDiffProvider; folding =
/// DefaultBranchResolver and the Azure DevOps discovery (empty to null); anywhere = AzureReposOpenPullRequests.
/// </summary>
public sealed class BranchRefTests
{
    [Theory]
    [InlineData(null, null, "all: null")]
    [InlineData("", "", "leading, anywhere: empty; folding: null (kept by the folding callers)")]
    [InlineData("refs/heads/", "", "all: empty — the folding callers now read it as no branch")]
    [InlineData("refs/heads/main", "main", "all: main")]
    [InlineData("refs/heads/feature/x", "feature/x", "all: feature/x")]
    [InlineData("main", "main", "all: main")]
    [InlineData("feature/refs/heads/x", "feature/refs/heads/x", "anywhere: feature/x")]
    [InlineData("refs/heads/refs/heads/x", "refs/heads/x", "anywhere: x")]
    [InlineData("refs/tags/v1", "refs/tags/v1", "all: unchanged")]
    [InlineData("REFS/HEADS/main", "REFS/HEADS/main", "all: unchanged, the match is ordinal")]
    public void ShortName_TheMatrix(string? reference, string? expected, string was) =>
        BranchRef.ShortName(reference).Should().Be(expected, was);

    [Theory]
    [InlineData("refs/heads/", "develop")]
    [InlineData("", "develop")]
    [InlineData("refs/heads/trunk", "trunk")]
    public async Task DefaultBranchResolver_ABareOrEmptyRef_IsNoAnswer_SoTheConfiguredBranchApplies(string answer, string expected)
    {
        var sut = new DefaultBranchResolver("develop", "repo", NullLogger.Instance);

        (await sut.ResolveAsync(_ => Task.FromResult<string?>(answer), CancellationToken.None)).Should().Be(expected,
            "a bare refs/heads/ used to resolve to an empty default branch");
    }
}
