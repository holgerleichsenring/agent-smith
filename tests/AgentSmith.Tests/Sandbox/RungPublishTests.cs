using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-13-35a4: a feature's branch is published exactly once per repository, and the
/// push that publishes it carries NO force and NO lease — the one property that separates
/// this path from the work-branch pusher, whose stale-info retry would force a shared
/// branch back to the base and delete everything already merged into it.
/// </summary>
public sealed class RungPublishTests
{
    private const string ParentTicket = "4711";
    private const string Rung = "agent-smith/4711";

    private static readonly RepoConnection Repo =
        new() { Name = "server", Type = RepoType.GitHub, Url = "https://example.invalid/repo.git" };

    /// <summary>
    /// A full clone that can also be pushed to: it answers ref checks from the branches it
    /// knows, and a successful push teaches it the branch it just created.
    /// </summary>
    private sealed class PushSandbox(params string[] remoteBranches) : ISandbox
    {
        private readonly List<string> _branches = [.. remoteBranches];

        public List<IReadOnlyList<string>> Ran { get; } = [];

        public string JobId => "slice";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            var args = step.Args ?? [];
            Ran.Add(args);
            var (exit, output) = Answer(args);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0.1, null, output));
        }

        public IReadOnlyList<string>? Pushed => Ran.FirstOrDefault(a => a.Contains("push"));

        private (int Exit, string Output) Answer(IReadOnlyList<string> args)
        {
            if (args.Contains("symbolic-ref")) return (0, "origin/main");
            if (args.Contains("push"))
            {
                _branches.Add(Rung);
                return (0, string.Empty);
            }
            if (args.Contains("fetch")) return (0, string.Empty);
            var wanted = args[^1];
            return _branches.Any(b => wanted == $"refs/remotes/origin/{b}")
                ? (0, "deadbeef")
                : (1, string.Empty);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task RungPublish_AlreadyExists_AdoptsItAndDoesNotPush()
    {
        var sandbox = new PushSandbox("main", Rung);

        var resolved = await TestGit.RungPublisher.EnsureAsync(
            sandbox, Repo, ParentTicket, CancellationToken.None);

        resolved.Name.Should().Be(Rung);
        resolved.FellThrough.Should().BeFalse();
        sandbox.Pushed.Should().BeNull(
            "a branch that is already there is adopted — publishing is idempotent, and the "
            + "cheapest idempotence is not pushing at all");
    }

    [Fact]
    public async Task RungPublish_NeverUsesForceOrLease()
    {
        var sandbox = new PushSandbox("main");

        await TestGit.RungPublisher.EnsureAsync(sandbox, Repo, ParentTicket, CancellationToken.None);

        var pushed = sandbox.Pushed;
        pushed.Should().NotBeNull("the rung is absent, so this slice publishes it");
        pushed.Should().NotContain(a => a.StartsWith("--force", StringComparison.Ordinal) || a == "-f",
            "a lease that can be refreshed by a fetch is how a shared branch loses the work "
            + "merged into it — the rejection IS the answer here");
        pushed.Should().Contain($"origin/main:refs/heads/{Rung}",
            "the rung is created at the sha of the ladder's next rung down");
    }

    [Fact]
    public async Task RungPublish_NoParentStamp_PublishesNothing()
    {
        var sandbox = new PushSandbox("main");

        var resolved = await TestGit.RungPublisher.EnsureAsync(
            sandbox, Repo, parentTicketId: null, CancellationToken.None);

        resolved.Name.Should().Be("main");
        resolved.FellThrough.Should().BeTrue();
        sandbox.Pushed.Should().BeNull(
            "a ticket that is not a slice has no feature to publish, so a repository no "
            + "slice touches never grows a branch");
    }

    [Fact]
    public async Task RungPublish_PublishedRung_CarriesTheFrameworkBranchConvention()
    {
        var sandbox = new PushSandbox("main");

        var resolved = await TestGit.RungPublisher.EnsureAsync(
            sandbox, Repo, ParentTicket, CancellationToken.None);

        resolved.Name.Should().Be(TicketBranchNamer.Compose(ParentTicket).Value,
            "the name the publisher composes must be the name a later slice looks for");
    }
}
