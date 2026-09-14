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

            var index = _script.FindIndex(entry => entry.Command == word);
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
