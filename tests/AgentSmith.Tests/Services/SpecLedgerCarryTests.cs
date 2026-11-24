using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Progress;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-11-24-0fbfa: a later master pass of one spec opens on the ledger the spec's earlier pass left; any other
/// master step is seeded from its spec's steps, all pending.
/// </summary>
public sealed class SpecLedgerCarryTests
{
    private static SpecDraft Draft(string specId) => new(specId, "goal", "spec: x\n", [])
    {
        Steps = [new SpecStep("s1", "first", "src/A.cs"), new SpecStep("s2", "second", null)],
    };

    private static PipelineContext Held(string specId)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProgressLedger, new ProgressLedger(
        [
            new ProgressLedgerEntry("s1", "first", ProgressStatus.Done, "src/A.cs"),
            new ProgressLedgerEntry("s2", "second", ProgressStatus.Done),
        ]));
        SpecLedgerCarry.Own(pipeline, Draft(specId));
        return pipeline;
    }

    [Fact]
    public void Seed_SameSpecsLedgerHeld_CarriesItAsItStands()
    {
        var seed = SpecLedgerCarry.Seed(Held("spec-a"), Draft("spec-a"));

        seed.Select(e => e.Status).Should().Equal(ProgressStatus.Done, ProgressStatus.Done);
    }

    [Fact]
    public void Seed_AnotherSpecsLedgerHeld_SeedsFromTheSteps()
    {
        var seed = SpecLedgerCarry.Seed(Held("spec-a"), Draft("spec-b"));

        seed.Select(e => (e.Id, e.Status)).Should().Equal(("s1", ProgressStatus.Pending), ("s2", ProgressStatus.Pending));
    }

    [Fact]
    public void Seed_NoLedgerHeld_SeedsFromTheSteps()
    {
        var seed = SpecLedgerCarry.Seed(new PipelineContext(), Draft("spec-a"));

        seed.Should().OnlyContain(e => e.Status == ProgressStatus.Pending).And.HaveCount(2);
    }

    [Fact]
    public void Seed_SameSpecWithoutSteps_CarriesTheMastersOwnEntries()
    {
        var draft = new SpecDraft("spec-a", "goal", "spec: x\n", []);
        var pipeline = Held("spec-a");
        SpecLedgerCarry.Own(pipeline, draft);

        SpecLedgerCarry.Seed(pipeline, draft).Should().HaveCount(2).And.OnlyContain(e => e.Status == ProgressStatus.Done);
    }

    [Fact]
    public void Seed_SameSpecRevisedSinceItsLedger_SeedsFromTheRevisedSteps()
    {
        var revised = Draft("spec-a") with { Steps = [new SpecStep("s1", "first", "src/A.cs"), new SpecStep("s3", "third", null)] };

        var seed = SpecLedgerCarry.Seed(Held("spec-a"), revised);

        seed.Select(e => (e.Id, e.Status)).Should().Equal(("s1", ProgressStatus.Pending), ("s3", ProgressStatus.Pending));
    }

    [Fact]
    public void Seed_NoSpec_IsEmpty()
    {
        SpecLedgerCarry.Seed(Held("spec-a"), null).Should().BeEmpty();
    }

    [Fact]
    public void Own_NoSpec_ForgetsTheOwner()
    {
        var pipeline = Held("spec-a");

        SpecLedgerCarry.Own(pipeline, null);

        pipeline.Has(ContextKeys.ProgressLedgerSpecId).Should().BeFalse();
        SpecLedgerCarry.Seed(pipeline, Draft("spec-a")).Should().OnlyContain(e => e.Status == ProgressStatus.Pending);
    }
}
