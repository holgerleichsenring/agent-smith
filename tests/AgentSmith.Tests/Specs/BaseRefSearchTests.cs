using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-08-25-0eae: the account can read the BASE, not only the branch.
/// <para>
/// Two of the three criteria a live two-repo migration failed on were quantified over the
/// base — "where X was previously configured" and "no host gains a transport it did not have
/// before" — and the account had no way to look there at all, so the default rule turned a
/// vacuous conditional into a refusal. This is reach only: what a base search may CONCLUDE
/// is its own spec.
/// </para>
/// </summary>
public sealed class BaseRefSearchTests
{
    private const string Repo = "Sample.Server";

    private sealed class RecordingSandbox(int exitCode, string? output = "") : ISandbox
    {
        public string JobId => "base";
        public List<Step> Ran { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Ran.Add(step);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, exitCode, false, 0.1, null, output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static BranchSearch For(ISandbox sandbox, string? baseRef) =>
        new(new Dictionary<string, ISandbox> { [Repo] = sandbox }, NullLogger.Instance,
            new Dictionary<string, string?> { [Repo] = baseRef });

    [Fact]
    public async Task BranchSearch_ACarriedBaseRef_IsSearchedReadOnly()
    {
        var sandbox = new RecordingSandbox(1);

        await For(sandbox, "origin/main").SearchBase(Repo, "ServiceBus");

        var step = sandbox.Ran.Should().ContainSingle().Subject;
        step.Command.Should().Be("git");
        step.Args.Should().ContainInOrder("grep", "-InE", "-e", "ServiceBus", "origin/main");
        step.Args.Should().Contain(":(exclude)**/bin/**");
    }

    [Fact]
    public async Task BranchSearch_ABaseSearchThatFoundNothing_IsRememberedAsEvidence()
    {
        var search = For(new RecordingSandbox(1), "origin/main");

        await search.SearchBase(Repo, "ServiceBus");

        search.Evidence.Should().ContainSingle()
            .Which.Should().Contain("'ServiceBus'").And.Contain("exited 1");
    }

    /// <summary>A citation that cannot tell the two apart cannot carry a rule that depends on
    /// which one ran — which is what every successor of this spec does.</summary>
    [Fact]
    public async Task BranchSearch_ABaseSearchEvidenceLine_NamesTheRefItRead()
    {
        var search = For(new RecordingSandbox(1), "origin/main");

        await search.SearchBase(Repo, "ServiceBus");
        await search.SearchBranch(Repo, "ServiceBus");

        search.Evidence.Should().HaveCount(2);
        search.Evidence[0].Should().StartWith($"{Repo}@origin/main:");
        search.Evidence[1].Should().StartWith($"{Repo}:");
    }

    [Fact]
    public async Task BranchSearch_ABranchAndABaseSearchOfOnePattern_AreTwoCitations()
    {
        var search = For(new RecordingSandbox(1), "origin/main");

        await search.SearchBase(Repo, "ServiceBus");
        await search.SearchBranch(Repo, "ServiceBus");

        search.Evidence.Distinct(StringComparer.Ordinal).Should().HaveCount(2);
    }

    /// <summary>An exit above 1 is a search that could not run. Folding it in with "found
    /// nothing" would let a broken search prove an absence.</summary>
    [Fact]
    public async Task BranchSearch_ASearchThatErrored_IsNotEvidenceOfAbsence()
    {
        var search = For(new RecordingSandbox(128, "fatal: bad revision"), "origin/main");

        var answer = await search.SearchBase(Repo, "ServiceBus");

        answer.Should().Contain("could not run").And.Contain("proves nothing");
        // p0484 keeps its rule: a search that HAPPENED is recorded, so an account that looked
        // may say so. What is added is that the line says it could not decide, so nothing
        // downstream can read it as an absence.
        search.Evidence.Should().ContainSingle()
            .Which.Should().Contain("exited 128").And.Contain("could not run, so it proves nothing");
    }

    [Fact]
    public async Task BranchSearch_ARepositoryWithNoBase_IsRefusedRatherThanSubstituted()
    {
        var sandbox = new RecordingSandbox(1);

        var answer = await For(sandbox, baseRef: null).SearchBase(Repo, "ServiceBus");

        answer.Should().Contain("has no base to search").And.Contain("proves nothing");
        sandbox.Ran.Should().BeEmpty("searching the branch under the name of the base is worse than not searching");
    }

    [Fact]
    public void BranchSearch_OnlyRepositoriesWithAResolvedBase_AreBaseSearchable()
    {
        var sandbox = new RecordingSandbox(1);
        var search = new BranchSearch(
            new Dictionary<string, ISandbox> { [Repo] = sandbox, ["Sample.Worker"] = sandbox },
            NullLogger.Instance,
            new Dictionary<string, string?> { [Repo] = "origin/main", ["Sample.Worker"] = null });

        search.Repositories.Should().HaveCount(2);
        search.BaseSearchable.Should().ContainSingle().Which.Should().Be(Repo);
    }

    /// <summary>A tool nobody is told about ships inert — the lesson p0483 wrote down.</summary>
}
