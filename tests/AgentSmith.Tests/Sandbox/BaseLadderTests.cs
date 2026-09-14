using AgentSmith.Application.Services;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-13-5cdf: one base per REPOSITORY — the parent's rung when this clone has it,
/// the clone's own base when it does not, and the fall-through says so rather than
/// presenting origin/HEAD as a rung it found.
/// </summary>
public sealed class BaseLadderTests
{
    private const string ParentTicket = "4711";
    private const string Rung = "agent-smith/4711";

    /// <summary>
    /// Answers ref checks like a full clone: it holds the branches it was given, and
    /// origin/HEAD names its own base.
    /// </summary>
    private sealed class RefSandbox(string jobId, params string[] remoteBranches) : ISandbox
    {
        public string JobId => jobId;
        public List<IReadOnlyList<string>> Ran { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            var args = step.Args ?? [];
            Ran.Add(args);
            var (exit, output) = Answer(args);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0.1, null, output));
        }

        private (int Exit, string Output) Answer(IReadOnlyList<string> args)
        {
            if (args.Contains("symbolic-ref")) return (0, "origin/main");
            var wanted = args[^1];
            return remoteBranches.Any(b => wanted == $"refs/remotes/origin/{b}")
                ? (0, "deadbeef")
                : (1, string.Empty);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task BaseLadder_ParentRungExists_IsChosen()
    {
        var sandbox = new RefSandbox("one", "main", Rung);

        var resolved = await TestGit.BaseLadder.ResolveAsync(sandbox, ParentTicket, CancellationToken.None);

        resolved.Name.Should().Be(Rung);
        resolved.Ref.Should().Be($"origin/{Rung}");
        resolved.FellThrough.Should().BeFalse("the rung exists, so nothing was fallen back to");
        sandbox.Ran.Should().NotContain(a => a.Contains("symbolic-ref"),
            "a ladder that found its rung does not need the clone's own base");
    }

    [Fact]
    public async Task BaseLadder_ParentRungAbsent_FallsThroughAndReportsIt()
    {
        var sandbox = new RefSandbox("one", "main");

        var resolved = await TestGit.BaseLadder.ResolveAsync(sandbox, ParentTicket, CancellationToken.None);

        resolved.Name.Should().Be("main", "the clone's own base is the bottom rung");
        resolved.FellThrough.Should().BeTrue(
            "2026-08-25-0eae: a ladder that reaches its bottom says so instead of claiming a rung");
        sandbox.Ran.Should().Contain(a => a.Contains($"refs/remotes/origin/{Rung}"),
            "the rung is looked for before it is given up on");
    }

    [Fact]
    public async Task BaseLadder_NoParentStamp_ResolvesAsToday()
    {
        var sandbox = new RefSandbox("one", "main", Rung);

        var resolved = await TestGit.BaseLadder.ResolveAsync(
            sandbox, parentTicketId: null, CancellationToken.None);

        resolved.Name.Should().Be("main");
        resolved.FellThrough.Should().BeTrue();
        sandbox.Ran.Should().NotContain(a => a.Contains("rev-parse"),
            "a ticket that is not a slice has no rung to look for — this is exactly today's cost");
    }

    [Fact]
    public async Task BaseLadder_TwoRepos_ResolvesEachIndependently()
    {
        // A feature may exist in one repository of a project and not in another.
        var withRung = new RefSandbox("server", "main", Rung);
        var without = new RefSandbox("worker", "main");
        var ladder = TestGit.BaseLadder;

        var first = await ladder.ResolveAsync(withRung, ParentTicket, CancellationToken.None);
        var second = await ladder.ResolveAsync(without, ParentTicket, CancellationToken.None);

        first.Name.Should().Be(Rung);
        second.Name.Should().Be("main");
        second.FellThrough.Should().BeTrue(
            "the rung is per repository — one answer for the run would send this one to the wrong base");
    }

    [Fact]
    public async Task BaseLadder_TheRungCarriesTheFrameworkBranchConvention()
    {
        var sandbox = new RefSandbox("one", "main", TicketBranchNamer.Compose(ParentTicket).Value);

        var resolved = await TestGit.BaseLadder.ResolveAsync(sandbox, ParentTicket, CancellationToken.None);

        resolved.Name.Should().Be(TicketBranchNamer.Compose(ParentTicket).Value,
            "the name a slice looks for must be the name the publisher will compose");
    }
}
