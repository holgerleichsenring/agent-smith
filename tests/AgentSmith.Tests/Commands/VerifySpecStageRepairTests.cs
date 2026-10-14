using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// A declared stage that ran red earns the spec's one repair pass with its output, over real
/// clones: never twice, never in the review's fix pass, never beside a resolution finding.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class VerifySpecStageRepairTests
{
    private const string SpecId = "2026-10-13-8762c";
    private const string RedStage = "grep -q 'app = 2' web/app.ts || { echo 'app is still 1'; exit 1; }";

    [Fact]
    public async Task VerifySpec_RedDeclaredStage_SplicesOneRepairPassCarryingTheStageOutput()
    {
        await using var clones = await Clones(RedStage);
        if (clones is null) return;

        var result = await Verify(clones);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.InsertNext.Should().HaveCount(SpecVerdict.RepairSteps(SpecId).Count).And.OnlyContain(c => c.SpecId == SpecId);
        clones.Pipeline.Get<List<string>>(ContextKeys.OutstandingCriteria).Should().ContainSingle()
            .Which.Should().StartWith(StageRepair.ItemPrefix).And.Contain("exited 1").And.Contain("app is still 1");
        clones.Pipeline.Get<bool>(ContextKeys.SpecRepairAttempted).Should().BeTrue();
    }

    [Fact]
    public async Task VerifySpec_RedDeclaredStage_ContinuationMessageStartsWithTheRedStagesLine()
    {
        await using var clones = await Clones(RedStage);
        if (clones is null) return;

        var result = await Verify(clones);

        result.Message.Should().StartWith("Verification failed: ").And.Contain("exited 1")
            .And.EndWith("; one repair pass follows").And.NotContain("\n");
    }

    [Fact]
    public async Task VerifySpec_RedStageAfterItsRepair_FailsTheSpec()
    {
        await using var clones = await Clones(RedStage);
        if (clones is null) return;
        clones.Pipeline.Set(ContextKeys.SpecRepairAttempted, true);

        var result = await Verify(clones);

        result.IsSuccess.Should().BeFalse();
        result.InsertNext.Should().BeNullOrEmpty();
        result.Message.Should().Contain("app is still 1");
    }

    [Fact]
    public async Task VerifySpec_RedStageInReviewFixPass_IsRevertedNotRepaired()
    {
        await using var clones = await Clones("grep -q 'app = 1' web/app.ts");
        if (clones is null) return;
        await clones.CommitAllAsync(clones.Primary, "the spec's work");
        (await Verify(clones)).IsSuccess.Should().BeTrue();
        SpecReviewFixPass.Prepare(clones.Pipeline);
        await clones.WriteAsync(clones.Primary, "web/app.ts", "export const app = 3;\n");

        var result = await Verify(clones);

        result.InsertNext.Should().BeNullOrEmpty("the review's fix pass already spent the repair");
        clones.Pipeline.TryGet<string>(ContextKeys.SpecReviewReverted, out var reverted).Should().BeTrue(result.Message);
        reverted.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task VerifySpec_ResolutionFailure_IsNotRepaired()
    {
        await using var clones = await Clones("echo all good");
        if (clones is null) return;

        var result = await Verify(clones);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("resolution failure");
        result.InsertNext.Should().BeNullOrEmpty();
        clones.Pipeline.TryGet<bool>(ContextKeys.SpecRepairAttempted, out _).Should().BeFalse();
    }

    [Fact]
    public async Task VerifySpec_ResolutionFailure_IsNotRepaired_BesideARedStageOfAnotherRepository()
    {
        await using var clones = await Clones(RedStage, secondRepository: "echo all good");
        if (clones is null) return;

        var result = await Verify(clones);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("resolution failure");
        result.InsertNext.Should().BeNullOrEmpty();
    }

    private static Task<CommandResult> Verify(SandboxClones clones) =>
        Handler().ExecuteAsync(new VerifySpecContext(new Dictionary<string, ProjectMap>(), clones.Pipeline), CancellationToken.None);

    /// <param name="secondRepository">A second sandbox, owned by a repository of its own, declaring this command.</param>
    private static async Task<SandboxClones?> Clones(string stage, string? secondRepository = null)
    {
        var clones = await SandboxClones.CreateAsync(count: secondRepository is null ? 1 : 2);
        if (clones is null) return null;
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int a; }\n");
        var contexts = new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
        {
            [clones.Primary] = [Declaring(stage)],
        };
        if (secondRepository is not null)
        {
            await clones.WriteAsync(clones.Secondary, "src/Api/Program.cs", "class Program { int b; }\n");
            contexts[clones.Secondary] = [Declaring(secondRepository)];
            clones.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
                new Dictionary<string, string> { [clones.Primary] = "shop", [clones.Secondary] = "web" });
            clones.Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
                [new RepoConnection { Name = "shop", Type = RepoType.Local }, new RepoConnection { Name = "web", Type = RepoType.Local }]);
        }
        clones.Pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            contexts.ToDictionary(c => c.Key, c => c.Value[0]));
        clones.Pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts, contexts);
        var draft = new SpecDraft(SpecId, "goal", $"spec: {SpecId}", []) { Done = ["the app is changed"] };
        clones.Pipeline.Set(ContextKeys.CurrentSpec, draft);
        clones.Pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig("code", new AgentConfig(), "skills", null));
        clones.Pipeline.Set(ContextKeys.SpecSet, new SpecSet("k", [new DerivedSpec(draft, SpecId, string.Empty, [])],
            SpecAccounting.Empty, [], SpecSource.Derived));
        return clones;
    }

    private static RemoteContextDiscovery Declaring(string command) =>
        new("default", ".", "csharp", Verify: [new ContextYamlVerifyStage("test", command)]);

    private static VerifySpecHandler Handler() => new(
        new VerifyStageResolver(
            new DeclaredStagePresence(new SandboxFileReaderFactory(), NullLogger<DeclaredStagePresence>.Instance),
            NullLogger<VerifyStageResolver>.Instance),
        new ContextVerifyStagesResolver(),
        new VerifyDerivationDrift(
            new VerifyDerivationDigest(new SandboxFileReaderFactory()), NullLogger<VerifyDerivationDrift>.Instance),
        new SandboxTargets(),
        TestTreeSync.Create(),
        TestTreeSync.Relevance(),
        new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance),
        TestGit.Delivery,
        new SpecDeliveryAccounting(TestGit.Delivery, new SatisfiedAccountant(), new SandboxTargets(),
            NullLogger<SpecDeliveryAccounting>.Instance),
        new SpecProgressRecorder(new NoOpEventPublisher()),
        new VerifiedHeads(NullLogger<VerifiedHeads>.Instance),
        TestSpecReview.Revert(),
        NullLogger<VerifySpecHandler>.Instance);

    private sealed class SatisfiedAccountant : ISpecAccountant
    {
        public Task<SpecAccount> AccountAsync(
            string repoKey, IReadOnlyList<string> criteria, string diff,
            IReadOnlyList<string> commandResults, AgentConfig agent,
            BranchSearch? branchSearch, PipelineCostTracker costTracker,
            CancellationToken cancellationToken,
            int windowBudgetChars = DiffWindows.DefaultBudgetChars) =>
            Task.FromResult(new SpecAccount(repoKey, [.. criteria.Select(c =>
                new CriterionAccount(c, AccountDisposition.Satisfied, "src/Api/Program.cs"))]));
    }
}
