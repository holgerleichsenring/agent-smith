using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0484: p0483 let the delivery account settle an absence by LOOKING and left it unable to
/// say that it had looked. The first live run ran fifteen searches across two repositories
/// and was then refused with "claimed satisfied but cited nothing", because a search the
/// ACCOUNT ran is neither a path in the diff nor a command in the listed evidence.
/// </summary>
public sealed class SearchedEvidenceTests
{
    private const string Repo = "Sample.Server";

    private sealed class SearchSandbox(int exitCode) : ISandbox
    {
        public string JobId => "searched";
        public List<Step> Ran { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Ran.Add(step);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode, false, 0.1, null, string.Empty));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static BranchSearch For(int exitCode) =>
        new(new Dictionary<string, ISandbox> { [Repo] = new SearchSandbox(exitCode) }, NullLogger.Instance);

    private static CriterionAccount Resolve(BranchSearch search, string citation) =>
        AccountTools.ResolverOver(string.Empty, [], search)
            .Resolve(new AccountRow("no MediatR remains", AccountDisposition.Satisfied, citation, "searched"));

    /// <summary>Exit 1 is the proof, so the line has to carry it — a reader that cannot see
    /// the status cannot tell an absence from a search that never ran.</summary>
    [Fact]
    public async Task BranchSearch_AnAbsenceProvingSearch_LeavesAnEvidenceLineCarryingExitOne()
    {
        var search = For(exitCode: 1);

        await search.SearchBranch(Repo, "MediatR");

        search.Evidence.Should().ContainSingle()
            .Which.Should().Be($"{Repo}: the account searched 'MediatR' exited 1");
    }

    [Fact]
    public async Task BranchSearch_ASearchThatCouldNotRun_IsStillRecorded()
    {
        var search = For(exitCode: 2);

        await search.SearchBranch(Repo, "MediatR", "nowhere");

        search.Evidence.Should().ContainSingle().Which.Should().Contain("exited 2",
            "a search that proves nothing is still a search that happened");
    }

    /// <summary>Neither reached the branch, so neither is evidence of anything.</summary>
    [Fact]
    public async Task BranchSearch_AnUnknownRepositoryOrBlankPattern_LeavesNoEvidenceLine()
    {
        var search = For(exitCode: 1);

        await search.SearchBranch("Sample.Worker", "MediatR");
        await search.SearchBranch(Repo, "   ");

        search.Evidence.Should().BeEmpty();
    }

    /// <summary>The live refusal, closed: the account searched and may now say so.</summary>
    [Fact]
    public async Task Citation_ThePatternOfASearchTheAccountRan_Resolves()
    {
        var search = For(exitCode: 1);
        await search.SearchBranch(Repo, "MediatR");

        Resolve(search, "MediatR").IsSatisfied.Should().BeTrue();
    }

    [Fact]
    public async Task Citation_APatternNobodySearchedFor_DoesNotResolve()
    {
        var search = For(exitCode: 1);
        await search.SearchBranch(Repo, "MediatR");

        Resolve(search, "MassTransit").IsSatisfied.Should().BeFalse(
            "the standard does not move: a citation naming no evidence still fails");
    }

    [Fact]
    public void Citation_WithNoSearchAtAll_StillNeedsAFileOrACommand()
    {
        AccountTools.ResolverOver(string.Empty, [], null)
            .Resolve(new AccountRow("no MediatR remains", AccountDisposition.Satisfied, "MediatR", "searched"))
            .IsSatisfied.Should().BeFalse();
    }

    [Fact]
    public void SpecAccountPrompt_WithSearchableRepositories_SaysHowToCiteASearch()
    {
        var prompt = SpecAccountPrompt.For(["no MediatR remains"], string.Empty, [], ["api"]);

        prompt.Should().Contain("CITED BY THE PATTERN")
            .And.Contain("searched and then cite nothing for is refused");
    }

    [Fact]
    public void AccountReAsk_Message_NamesTheSearchCitationForm()
    {
        var message = AccountReAsk.Message(
            [new CriterionAccount("no MediatR remains", AccountDisposition.NotSatisfied, null, "claimed satisfied but cited nothing")]);

        message.Should().Contain("search_branch").And.Contain("PATTERN");
    }
}
