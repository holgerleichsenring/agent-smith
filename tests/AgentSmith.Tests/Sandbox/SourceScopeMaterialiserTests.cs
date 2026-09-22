using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-13-9802: the git ladder that lands a read-only scope on a named revision, and
/// the refusals it tells apart. Four of these used to arrive as one sentence.
/// </summary>
public sealed class SourceScopeMaterialiserTests
{
    private static readonly RepoConnection Repo = new()
    {
        Name = "repo-a", Type = RepoType.GitHub, Url = "https://stub.test/repo-a",
    };

    [Fact]
    public async Task Prepare_NoRevision_ClonesAndNeverChecksOut()
    {
        var sandbox = new ScriptedSandbox().Returning("rev-parse", 0, "abc123");

        var sha = await new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, revision: null, CancellationToken.None);

        sha.Should().Be("abc123");
        sandbox.Commands.Should().Equal("clone", "rev-parse");
    }

    [Fact]
    public async Task Prepare_RevisionGiven_ChecksOutAndReportsTheSha()
    {
        var sandbox = new ScriptedSandbox().Returning("rev-parse", 0, "deadbeef");

        var sha = await new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, "v2.1.0", CancellationToken.None);

        sha.Should().Be("deadbeef");
        sandbox.Commands.Should().Equal("clone", "checkout", "rev-parse");
    }

    [Fact]
    public async Task Prepare_UnknownRevision_ReportsNoSuchRevision()
    {
        var sandbox = new ScriptedSandbox()
            .Returning("checkout", 1, "error: pathspec 'nope' did not match")
            .Returning("fetch", 0, string.Empty);

        var act = () => new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, "nope", CancellationToken.None);

        var failure = await act.Should().ThrowAsync<SourceScopeUnavailableException>();
        failure.Which.Kind.Should().Be(SourceScopeFailureKind.NoSuchRevision);
        failure.Which.Revision.Should().Be("nope");
        failure.Which.RepoName.Should().Be("repo-a");
    }

    [Fact]
    public async Task Prepare_ShaReachableFromNoRef_ReportsRevisionNotFetched()
    {
        var sandbox = new ScriptedSandbox()
            .Returning("checkout", 1, "fatal: reference is not a tree")
            .Returning("fetch", 1, "error: couldn't find remote ref 9f1c2d");

        var act = () => new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, "9f1c2d", CancellationToken.None);

        var failure = await act.Should().ThrowAsync<SourceScopeUnavailableException>();
        failure.Which.Kind.Should().Be(SourceScopeFailureKind.RevisionNotFetched);
    }

    [Fact]
    public async Task Prepare_ShaFetchedOnRequest_Succeeds()
    {
        var sandbox = new ScriptedSandbox()
            .ReturningOnce("checkout", 1, "fatal: reference is not a tree")
            .Returning("fetch", 0, string.Empty)
            .Returning("rev-parse", 0, "9f1c2d");

        var sha = await new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, "9f1c2d", CancellationToken.None);

        sha.Should().Be("9f1c2d");
        sandbox.Commands.Should().Equal("clone", "checkout", "fetch", "checkout", "rev-parse");
    }

    [Theory]
    [InlineData("fatal: Authentication failed for 'https://stub.test/repo-a'")]
    [InlineData("remote: Repository not found.")]
    [InlineData("fatal: could not read Username for 'https://stub.test': terminal prompts disabled")]
    public async Task Prepare_HostRefusesTheCredential_ReportsUnauthorised(string stderr)
    {
        var sandbox = new ScriptedSandbox().Returning("clone", 128, stderr);

        var act = () => new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, null, CancellationToken.None);

        (await act.Should().ThrowAsync<SourceScopeUnavailableException>())
            .Which.Kind.Should().Be(SourceScopeFailureKind.Unauthorised);
    }

    [Fact]
    public async Task Prepare_HostUnreachable_ReportsUnreachable()
    {
        var sandbox = new ScriptedSandbox()
            .Returning("clone", 128, "fatal: unable to access: Could not resolve host: stub.test");

        var act = () => new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, null, CancellationToken.None);

        (await act.Should().ThrowAsync<SourceScopeUnavailableException>())
            .Which.Kind.Should().Be(SourceScopeFailureKind.Unreachable);
    }

    [Fact]
    public async Task Prepare_RevisionCheckoutFails_NeverCreatesABranch()
    {
        var sandbox = new ScriptedSandbox()
            .Returning("checkout", 1, "error: pathspec did not match")
            .Returning("fetch", 0, string.Empty);

        try
        {
            await new SourceScopeMaterialiser()
                .PrepareAsync(sandbox, Repo, "nope", CancellationToken.None);
        }
        catch (SourceScopeUnavailableException)
        {
            // the refusal is asserted elsewhere; what matters here is what was NOT run
        }

        sandbox.AllArgs.Should().NotContain(
            args => args.Contains("-b"),
            "a failed revision must never become a new branch at the default HEAD");
    }


    // ---- 2026-09-22-b41d: the scope's clone is one branch at one commit ----

    [Fact]
    public async Task SourceScope_AScopeWithNoNamedRevision_ClonesOneBranchAtOneCommit()
    {
        var sandbox = new ScriptedSandbox().Returning("rev-parse", 0, "abc123");

        await new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, revision: null, CancellationToken.None);

        var clone = sandbox.AllArgs.Single(args => args.Contains("clone"));
        clone.Should().ContainInOrder("--depth", "1")
            .And.Contain("--single-branch",
                "a read-only scope reads files at one commit and never asks for history");
    }

    [Fact]
    public async Task SourceScope_AScopeNamingAReachableRevision_LandsOnIt()
    {
        var sandbox = new ScriptedSandbox().Returning("rev-parse", 0, "deadbeef");

        var sha = await new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, "v2.1.0", CancellationToken.None);

        sha.Should().Be("deadbeef");
        sandbox.Commands.Should().Equal("clone", "checkout", "rev-parse");
    }

    [Fact]
    public async Task SourceScope_AScopeNamingARevisionTheNarrowCloneLacks_FetchesItByName()
    {
        var sandbox = new ScriptedSandbox()
            .ReturningOnce("checkout", 1, "fatal: reference is not a tree")
            .Returning("fetch", 0, string.Empty)
            .Returning("rev-parse", 0, "9f1c2d");

        var sha = await new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, "9f1c2d", CancellationToken.None);

        sha.Should().Be("9f1c2d");
        sandbox.Commands.Should().Equal("clone", "checkout", "fetch", "checkout", "rev-parse");
        sandbox.AllArgs.Should().NotContain(
            args => args.Contains("fetch") && args.Contains("--depth"),
            "the rung that already existed lands it, so the depth rung is never reached");
    }

    [Fact]
    public async Task SourceScope_ADepthFetchLandsTheRevision_ChecksOutWhatCameBack()
    {
        var sandbox = new ScriptedSandbox()
            .Returning("FETCH_HEAD", 0, string.Empty)
            .Returning("checkout", 1, "error: pathspec 'release/7' did not match")
            .Returning("fetch", 0, string.Empty)
            .Returning("rev-parse", 0, "77c0de");

        var sha = await new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, "release/7", CancellationToken.None);

        sha.Should().Be("77c0de");
        sandbox.AllArgs.Should().Contain(
            args => args.Contains("fetch") && args.Contains("--depth"),
            "a single-branch clone tracks no other remote ref, so the revision is asked "
            + "for with depth and the tree lands on what came back");
        sandbox.AllArgs.Should().Contain(args => args.Contains("FETCH_HEAD"));
    }

    [Fact]
    public async Task SourceScope_AScopeNamingARevisionNoFetchCanLand_IsRefusedAsAMissingRevision()
    {
        var sandbox = new ScriptedSandbox()
            .Returning("checkout", 1, "fatal: reference is not a tree")
            .Returning("fetch", 1, "error: couldn't find remote ref 9f1c2d");

        var act = () => new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, "9f1c2d", CancellationToken.None);

        var failure = await act.Should().ThrowAsync<SourceScopeUnavailableException>();
        failure.Which.Kind.Should().Be(SourceScopeFailureKind.RevisionNotFetched);
        sandbox.AllArgs.Should().Contain(
            args => args.Contains("fetch") && args.Contains("--depth"),
            "the revision is asked for with depth BEFORE anything is refused");
    }

    [Fact]
    public async Task SourceScope_AnUnreachableHost_IsStillRefusedAsUnreachable()
    {
        var sandbox = new ScriptedSandbox()
            .Returning("clone", 128, "fatal: unable to access: Could not resolve host: stub.test");

        var act = () => new SourceScopeMaterialiser()
            .PrepareAsync(sandbox, Repo, "v2.1.0", CancellationToken.None);

        (await act.Should().ThrowAsync<SourceScopeUnavailableException>())
            .Which.Kind.Should().Be(SourceScopeFailureKind.Unreachable);
        // A dead host is not a revision hunt: the narrow clone must not turn an unreachable
        // host into a missing revision, so nothing runs after the clone that failed.
        sandbox.Commands.Should().Equal(["clone"]);
    }

    /// <summary>
    /// The scope's vocabulary is the proof that a shallow clone is enough: a log, a blame, a
    /// diff and a merge base are all Run steps, and a scope refuses every kind but the four
    /// reads — so nothing that addresses one can ask the clone for history.
    /// </summary>
    [Fact]
    public void SourceScope_TheFourReads_AreUnchangedAgainstANarrowClone()
    {
        var served = Enum.GetValues<StepKind>()
            .Where(kind => SourceScopeRefusal.UnlessRead(Step(kind)) is null)
            .ToList();

        served.Should().BeEquivalentTo(
            [StepKind.ReadFile, StepKind.ListFiles, StepKind.Grep, StepKind.DirectoryTree],
            "a scope serves four reads and refuses everything else, StepKind.Run included");
    }

    private static Step Step(StepKind kind) =>
        new(AgentSmith.Sandbox.Wire.Step.CurrentSchemaVersion, Guid.NewGuid(), kind);

    /// <summary>An ISandbox whose git results are scripted per command word.</summary>
    private sealed class ScriptedSandbox : ISandbox
    {
        private readonly List<(string Command, int Exit, string Text, bool Once)> _script = [];
        public List<string> Commands { get; } = [];
        public List<IReadOnlyList<string>> AllArgs { get; } = [];
        public string JobId => "scripted";

        public ScriptedSandbox Returning(string command, int exit, string text)
        {
            _script.Add((command, exit, text, false));
            return this;
        }

        public ScriptedSandbox ReturningOnce(string command, int exit, string text)
        {
            _script.Add((command, exit, text, true));
            return this;
        }

        public Task<StepResult> RunStepAsync(
            Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
        {
            var args = step.Args ?? [];
            AllArgs.Add(args);
            var word = args.FirstOrDefault(a =>
                a is "clone" or "checkout" or "fetch" or "rev-parse") ?? "?";
            Commands.Add(word);

            // Matched on any arg, so a rung can be scripted by the token that tells it apart
            // from its siblings (FETCH_HEAD, --depth) as well as by its git keyword. First
            // entry wins, so the narrower one is added first.
            var index = _script.FindIndex(entry => args.Contains(entry.Command));
            var (_, exit, text, once) = index >= 0 ? _script[index] : (word, 0, string.Empty, false);
            if (index >= 0 && once) _script.RemoveAt(index);

            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exit,
                TimedOut: false, DurationSeconds: 0.01,
                ErrorMessage: exit == 0 ? null : text,
                OutputContent: exit == 0 ? text : null));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
