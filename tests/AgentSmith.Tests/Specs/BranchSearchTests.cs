using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0483: the delivery account settles an absence by LOOKING at the branch, instead of being
/// shown a command that looked. Five phases fixed the form of a fact copied into a budgeted
/// list and copied back out; this removes the copying for the criterion class that has cost
/// the most runs.
/// </summary>
public sealed class BranchSearchTests
{
    private sealed class SearchSandbox(int exitCode, string? output) : ISandbox
    {
        public string JobId => "search";
        public List<Step> Ran { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Ran.Add(step);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode, false, 0.1, null, output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static BranchSearch For(SearchSandbox sandbox, string repo = "Sample.Server") =>
        new(new Dictionary<string, ISandbox> { [repo] = sandbox }, NullLogger.Instance);

    /// <summary>The whole point. grep exits 1 BECAUSE it found nothing, and a reader that
    /// reads that as a failed search has the proof backwards.</summary>
    [Fact]
    public async Task BranchSearch_FindingNothing_ReportsItAsTheProof()
    {
        var answer = await For(new SearchSandbox(1, string.Empty))
            .SearchBranch("Sample.Server", "MassTransit");

        answer.Should().Be("'MassTransit' does not occur anywhere in Sample.Server.");
    }

    [Fact]
    public async Task BranchSearch_FindingMatches_ReportsThemWithTheirLines()
    {
        var answer = await For(new SearchSandbox(0, "src/Api/Bus.cs:12:using MassTransit;"))
            .SearchBranch("Sample.Server", "MassTransit");

        answer.Should().StartWith("'MassTransit' found in Sample.Server:")
            .And.Contain("src/Api/Bus.cs:12");
    }

    /// <summary>A search that could not run proves nothing, which the account's prompt has
    /// said since p0469 and which this must not quietly contradict.</summary>
    [Fact]
    public async Task BranchSearch_ASearchThatCouldNotRun_SaysItProvesNothing()
    {
        var answer = await For(new SearchSandbox(2, "grep: no such directory"))
            .SearchBranch("Sample.Server", "MassTransit", "does/not/exist");

        answer.Should().Contain("proves nothing");
    }

    [Fact]
    public async Task BranchSearch_AnUnknownRepository_NamesTheOnesTheBranchCarries()
    {
        var answer = await For(new SearchSandbox(1, null))
            .SearchBranch("Sample.Worker", "MassTransit");

        answer.Should().Contain("No repository named 'Sample.Worker'").And.Contain("Sample.Server");
    }

    /// <summary>
    /// Read-only is a property of what CAN run here, not of what the caller is asked to stick
    /// to. The pattern is one element of a fixed argument vector, so nothing a model writes
    /// reaches a shell.
    /// </summary>
    [Fact]
    public async Task BranchSearch_RunsGrepWithAFixedArgumentVector_SoAPatternIsNeverACommand()
    {
        var sandbox = new SearchSandbox(1, null);

        await For(sandbox).SearchBranch("Sample.Server", "x'; rm -rf / #");

        var step = sandbox.Ran.Single();
        step.Command.Should().Be("grep");
        step.Args.Should().Contain("x'; rm -rf / #", "the pattern travels as one argv element");
        step.Args.Should().NotContain(a => a.Contains("rm -rf /", StringComparison.Ordinal) && a != "x'; rm -rf / #");
    }

    [Fact]
    public async Task BranchSearch_APatternThatIsBlank_IsRefusedWithoutRunningAnything()
    {
        var sandbox = new SearchSandbox(1, null);

        var answer = await For(sandbox).SearchBranch("Sample.Server", "  ");

        answer.Should().Be("A search needs a pattern.");
        sandbox.Ran.Should().BeEmpty();
    }

    /// <summary>Every search is a sandbox round-trip inside a model call at the end of a run,
    /// so the account gets a budget rather than a loop.</summary>
    [Fact]
    public async Task BranchSearch_MoreSearchesThanAllowed_AreRefused()
    {
        var sandbox = new SearchSandbox(1, null);
        var search = For(sandbox);

        for (var i = 0; i <= AccountSearchBudget.PerPass; i++)
            await search.SearchBranch("Sample.Server", $"pattern{i}");

        sandbox.Ran.Should().HaveCount(AccountSearchBudget.PerPass);
        (await search.SearchBranch("Sample.Server", "one more")).Should().Contain("No search left");
    }

    [Fact]
    public void BranchSearch_Repositories_AreTheOnesTheAccountMayName()
    {
        For(new SearchSandbox(1, null)).Repositories.Should().Equal("Sample.Server");
    }
}
