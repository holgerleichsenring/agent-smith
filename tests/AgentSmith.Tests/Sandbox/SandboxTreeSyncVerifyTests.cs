using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
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

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// The sync where it is used: VerifySpec levels a mixed-stack repository before its stages,
/// fails the spec on a conflict, reads one diff per repository afterwards, and a revert or a
/// shortfall after a green sync leaves nothing for the PR-time consolidation to fold.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class SandboxTreeSyncVerifyTests
{
    private const string SpecId = "2026-10-13-8762a";

    [Fact]
    public async Task VerifySpec_SyncConflict_FailsTheSpecAndRecordsTheProblem()
    {
        await using var clones = await Clones();
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int a; }\n");
        await clones.WriteAsync(clones.Secondary, "src/Api/Program.cs", "class Program { int b; }\n");
        var events = EventTestStubs.Recording();

        var result = await Handler(new RecordingAccountant(), events).ExecuteAsync(Context(clones), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("src/Api/Program.cs was changed in both");
        RunAccountLedger.Current(clones.Pipeline).All.Should().OnlyContain(a => a.Problem == result.Message);
        events.Events.OfType<SpecStateChangedEvent>().Single().State.Should().Be(SpecRunState.Failed);
    }

    [Fact]
    public async Task VerifySpec_AfterGreenSync_RecordsAVerifiedHeadForTheSecondary()
    {
        await using var clones = await Clones();
        if (clones is null) return;
        await clones.WriteAsync(clones.Secondary, "web/app.ts", "export const app = 2;\n");

        var result = await Handler(new RecordingAccountant()).ExecuteAsync(Context(clones), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        VerifiedHeads.For(clones.Pipeline, clones.Secondary).Should().Be(await clones.HeadAsync(clones.Secondary));
    }

    [Fact]
    public async Task VerifySpec_AfterGreenSync_AccountReadsOneDiffPerRepository()
    {
        await using var clones = await Clones();
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int c; }\n");
        var accountant = new RecordingAccountant();

        await Handler(accountant).ExecuteAsync(Context(clones), CancellationToken.None);

        accountant.Keys.Should().Equal(clones.Primary);
    }

    [Fact]
    public async Task SpecReviewRevert_MixedStackFixPassRedAfterGreenSync_SpecStaysDone_NextSyncIsLevel()
    {
        await using var clones = await Clones();
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int d; }\n");
        (await Sync(clones)).IsLevel.Should().BeTrue();
        await new VerifiedHeads(NullLogger<VerifiedHeads>.Instance)
            .RecordAsync(clones.Pipeline, clones.Sandboxes, CancellationToken.None);
        SpecReviewFixPass.Prepare(clones.Pipeline);
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int broken }\n");
        (await Sync(clones)).IsLevel.Should().BeTrue("the fix pass's own verification syncs first");

        var reverted = await TestSpecReview.Revert().TryRevertAsync(
            clones.Pipeline, CommandResult.Fail("Verification failed: build exited 1"), CancellationToken.None);

        reverted!.IsSuccess.Should().BeTrue(reverted.Message);
        (await clones.ReadAsync(clones.Secondary, "src/Api/Program.cs")).Should().Be("class Program { int d; }\n");
        (await Sync(clones)).IsLevel.Should().BeTrue();
        (await clones.HeadTreeAsync(clones.Secondary)).Should().Be(await clones.HeadTreeAsync(clones.Primary));
    }

    [Fact]
    public async Task ShortfallDelivery_AfterGreenSync_PrConsolidationFoldsNothing()
    {
        await using var clones = await Clones();
        if (clones is null) return;
        await clones.WriteAsync(clones.Primary, "src/Api/Program.cs", "class Program { int e; }\n");
        (await Sync(clones)).IsLevel.Should().BeTrue();
        await new VerifiedHeads(NullLogger<VerifiedHeads>.Instance)
            .RecordAsync(clones.Pipeline, clones.Sandboxes, CancellationToken.None);
        await clones.WriteAsync(clones.Primary, "web/app.ts", "export const unverified = true;\n");
        (await Sync(clones)).IsLevel.Should().BeTrue("a later spec's verification spread its work");
        clones.Pipeline.Set(ContextKeys.FailureReason, "budget exhausted");
        clones.Pipeline.Set(ContextKeys.SpecSequenceProgress, new SpecSequenceProgress(
        [
            new SpecProgress("p0001a", "Verified", SpecRunState.Done),
            new SpecProgress("p0001b", "Stopped", SpecRunState.InProgress),
        ]));
        var targets = new SandboxTargets();
        var shortfall = new ShortfallDelivery(
            new UnverifiedWorkReverter(TestTreeSync.GitOps(), targets, NullLogger<UnverifiedWorkReverter>.Instance),
            NullLogger<ShortfallDelivery>.Instance);

        var prepared = await shortfall.PrepareAsync(
            clones.Pipeline, [.. clones.Matches.Select(m => (m.Key, m.Value))], CancellationToken.None);
        var folded = await TestTreeSync.GitOps().ConsolidateSecondarySandboxesAsync(
            clones.Matches, clones.Sandboxes[clones.Primary], CancellationToken.None);

        prepared.Should().NotBeNull();
        folded.Should().Be(0, "the restored secondary committed its restore, so it carries nothing to fold");
        (await clones.ReadAsync(clones.Secondary, "web/app.ts")).Should().Be("export const app = 1;\n");
    }

    private static async Task<SandboxClones?> Clones()
    {
        var clones = await SandboxClones.CreateAsync();
        if (clones is null) return null;
        var contexts = new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal);
        var discoveries = new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal);
        foreach (var key in clones.Sandboxes.Keys)
        {
            discoveries[key] = new RemoteContextDiscovery(key, ".", "csharp",
                Verify: [new ContextYamlVerifyStage("build", "test -f web/app.ts")]);
            contexts[key] = [discoveries[key]];
        }
        clones.Pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries, discoveries);
        clones.Pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts, contexts);
        var draft = new SpecDraft(SpecId, "goal", $"spec: {SpecId}", []) { Done = ["the app is changed"] };
        clones.Pipeline.Set(ContextKeys.CurrentSpec, draft);
        clones.Pipeline.Set(ContextKeys.ResolvedPipeline, new ResolvedPipelineConfig("code", new AgentConfig(), "skills", null));
        clones.Pipeline.Set(ContextKeys.SpecSet, new SpecSet("k", [new DerivedSpec(draft, SpecId, string.Empty, [])],
            SpecAccounting.Empty, [], SpecSource.Derived));
        return clones;
    }

    private static Task<SandboxTreeSyncResult> Sync(SandboxClones clones) =>
        TestTreeSync.Create().SyncAsync(clones.Pipeline, CancellationToken.None);

    private static VerifySpecContext Context(SandboxClones clones) => new(new Dictionary<string, ProjectMap>(), clones.Pipeline);

    private static VerifySpecHandler Handler(ISpecAccountant accountant, IEventPublisher? events = null) => new(
        new VerifyStageResolver(
            new DeclaredStagePresence(new SandboxFileReaderFactory(), NullLogger<DeclaredStagePresence>.Instance),
            NullLogger<VerifyStageResolver>.Instance),
        new ContextVerifyStagesResolver(),
        new VerifyDerivationDrift(
            new VerifyDerivationDigest(new SandboxFileReaderFactory()), NullLogger<VerifyDerivationDrift>.Instance),
        new SandboxTargets(),
        TestTreeSync.Create(),
        new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance),
        TestGit.Delivery,
        new SpecDeliveryAccounting(TestGit.Delivery, accountant, new SandboxTargets(), NullLogger<SpecDeliveryAccounting>.Instance),
        new SpecProgressRecorder(events ?? new NoOpEventPublisher()),
        new VerifiedHeads(NullLogger<VerifiedHeads>.Instance),
        TestSpecReview.Revert(),
        NullLogger<VerifySpecHandler>.Instance);

    private sealed class RecordingAccountant : ISpecAccountant
    {
        public List<string> Keys { get; } = [];

        public Task<SpecAccount> AccountAsync(
            string repoKey, IReadOnlyList<string> criteria, string diff,
            IReadOnlyList<string> commandResults, AgentConfig agent,
            BranchSearch? branchSearch, PipelineCostTracker costTracker,
            CancellationToken cancellationToken,
            int windowBudgetChars = DiffWindows.DefaultBudgetChars)
        {
            Keys.Add(repoKey);
            return Task.FromResult(new SpecAccount(repoKey, [.. criteria.Select(c =>
                new CriterionAccount(c, AccountDisposition.Satisfied, "web/app.ts"))]));
        }
    }
}
