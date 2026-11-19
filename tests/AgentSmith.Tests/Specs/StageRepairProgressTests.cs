using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-18-9c1cc: a red stage goes back to the master while its repairs make progress — the repair wrote to the
/// tree and the failure changed — and not once the cost budget is spent. No count decides it.
/// </summary>
public sealed class StageRepairProgressTests
{
    private static readonly CommandResult Red = CommandResult.Fail("Verification failed: build exited 1");

    [Fact]
    public void FirstRed_GoesBackToTheMaster()
    {
        var pipeline = new PipelineContext();

        StageRepair.Offer(pipeline, [Outcome("error CS0103 at A.cs")], [], Red)!.InsertNext
            .Should().HaveCount(StageRepair.RepairBlock.Count);
        pipeline.Get<string>(ContextKeys.SpecRepairRed).Should().Contain("error CS0103 at A.cs");
    }

    [Fact]
    public void ADifferentRedAfterARepairThatWrote_GoesBackAgain()
    {
        var pipeline = new PipelineContext();
        StageRepair.Offer(pipeline, [Outcome("error CS0103 at A.cs")], [], Red);
        Wrote(pipeline);

        StageRepair.Offer(pipeline, [Outcome("error CS0246 at B.cs")], [], Red).Should().NotBeNull(
            "the repair changed the tree and the failure, so it is making progress");
    }

    [Fact]
    public void TheSameRedAfterARepairThatWrote_IsFinal()
    {
        var pipeline = new PipelineContext();
        StageRepair.Offer(pipeline, [Outcome("error CS0103 at A.cs")], [], Red);
        Wrote(pipeline);

        StageRepair.Offer(pipeline, [Outcome("error CS0103 at A.cs")], [], Red).Should().BeNull();
    }

    [Fact]
    public void ADifferentRedAfterARepairThatWroteNothing_IsFinal()
    {
        var pipeline = new PipelineContext();
        StageRepair.Offer(pipeline, [Outcome("error CS0103 at A.cs")], [], Red);
        pipeline.Set<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges, []);

        StageRepair.Offer(pipeline, [Outcome("error CS0246 at B.cs")], [], Red).Should().BeNull();
    }

    [Fact]
    public void ASpentBudget_IsFinalAtOnce()
    {
        var pipeline = new PipelineContext();
        pipeline.Set("PipelineCostCap", new CostCapValues { Usd = 1m, Tokens = -1 });

        StageRepair.Offer(pipeline, [Outcome("error CS0103 at A.cs")], [], Red).Should().BeNull();
    }

    private static VerifyOutcome Outcome(string output) =>
        new("shop", "build", "dotnet build", 1, Skipped: false, output, "/work");

    private static void Wrote(PipelineContext pipeline) =>
        pipeline.Set<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges,
            [new CodeChange(new FilePath("src/A.cs"), "fixed", "Modify")]);
}
