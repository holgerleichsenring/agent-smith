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
/// is its own phase.
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
    /// which one ran — which is what every successor of this phase does.</summary>
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
    [Fact]
    public void AccountTools_ABaseSearchableRepository_OffersTheBaseSearch()
    {
        var sandbox = new RecordingSandbox(1);

        AccountTools.For(For(sandbox, "origin/main"))!
            .Select(t => t.Name).Should().Contain("search_base");
        AccountTools.For(For(sandbox, baseRef: null))!
            .Select(t => t.Name).Should().NotContain("search_base",
                "a tool that answers 'no base' to every call teaches the account to stop calling it");
    }

    /// <summary>
    /// The wiring: the ref the delivery diff settled on is the ref handed to the search. A
    /// second resolution could pick a different one, and the account would then be reading a
    /// diff taken against a base it is not searching.
    /// </summary>
    [Fact]
    public async Task PhaseAccounting_TheResolvedBaseRef_ReachesTheSearch()
    {
        var capturing = new CapturingAccountant();
        var accounting = new AgentSmith.Application.Services.Specs.PhaseAccounting(
            AgentSmith.Tests.TestHelpers.TestGit.Delivery,
            capturing,
            new AgentSmith.Application.Services.Handlers.SandboxTargets(),
            NullLogger<AgentSmith.Application.Services.Specs.PhaseAccounting>.Instance);

        await accounting.TakeAsync(
            Pipeline(),
            new Dictionary<string, ISandbox> { [Repo] = new GitSpeakingSandbox() },
            [], CancellationToken.None);

        capturing.Search.Should().NotBeNull();
        capturing.Search!.BaseSearchable.Should().ContainSingle().Which.Should().Be(Repo);
    }

    private static AgentSmith.Contracts.Commands.PipelineContext Pipeline()
    {
        var pipeline = new AgentSmith.Contracts.Commands.PipelineContext();
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.PhaseSpec,
            new AgentSmith.Contracts.Models.PhaseDraft("p1", "goal", "phase: p1", [])
            { Done = ["a criterion"] });
        pipeline.Set(AgentSmith.Contracts.Commands.ContextKeys.ResolvedPipeline,
            new AgentSmith.Contracts.Models.Configuration.ResolvedPipelineConfig(
                "code", new AgentSmith.Contracts.Models.Configuration.AgentConfig(), "skills", null));
        return pipeline;
    }

    /// <summary>Answers like a clone: it names an origin/HEAD and it can diff against it.</summary>
    private sealed class GitSpeakingSandbox : ISandbox
    {
        public string JobId => "clone";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            var output = step.Args is not null && step.Args.Contains("symbolic-ref")
                ? "origin/main"
                : "diff --git a/x b/x\n--- a/x\n+++ b/x\n";
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CapturingAccountant : ISpecAccountant
    {
        public BranchSearch? Search { get; private set; }

        public Task<AgentSmith.Domain.Models.SpecAccount> AccountAsync(
            string repoKey, IReadOnlyList<string> criteria, string diff,
            IReadOnlyList<string> commandResults,
            AgentSmith.Contracts.Models.Configuration.AgentConfig agent,
            BranchSearch? branchSearch,
            AgentSmith.Application.Services.PipelineCostTracker costTracker,
            CancellationToken cancellationToken, int windowBudgetChars)
        {
            Search = branchSearch;
            return Task.FromResult(new AgentSmith.Domain.Models.SpecAccount(repoKey, []));
        }
    }

    [Fact]
    public void AccountPrompt_ABaseSearchableRepository_IsNamedInTheInstructions()
    {
        SpecAccountPrompt.For(["a criterion"], "", [], [Repo], null, [Repo])
            .Should().Contain("search_base").And.Contain("as it stood BEFORE this delivery");
        SpecAccountPrompt.For(["a criterion"], "", [], [Repo], null, [])
            .Should().NotContain("search_base");
    }
}
