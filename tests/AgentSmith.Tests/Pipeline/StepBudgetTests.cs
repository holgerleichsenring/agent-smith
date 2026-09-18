using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>
/// 2026-09-17-0e79e: the executor's loop guard is sized from the phases a run will
/// actually splice. The fixed 100 was sized for a pipeline whose length was known at
/// configuration time; a coding run's length follows the ticket's cut.
/// </summary>
public sealed class StepBudgetTests
{
    [Fact]
    public void StepBudget_EightPhases_ExceedsTheOldCeilingAndFitsTheNewOne()
    {
        // 22 literal commands + 8 phases x (6 block + 4 repair) = 102 today, and about 160
        // once the premise check and the review/fix pass join the block.
        var budget = StepBudget.ForPhases(SpecSet.MaxPhases);

        budget.Limit.Should().BeGreaterThan(StepBudget.Default,
            "a set at the phase cap does not fit the number a sequence-less preset has");
        budget.Limit.Should().BeGreaterThan(102, "that is what today's arithmetic already needs");
        budget.Limit.Should().BeGreaterThanOrEqualTo(160, "the premise check and the review pass are coming");
        budget.Limit.Should().BeLessThan(StepBudget.AbsoluteCeiling,
            "the computed number is the guard; the ceiling only clamps it");
    }

    [Fact]
    public void StepBudget_OnePhase_IsSmallerThanTheDefaultCeilingButSufficient()
    {
        // The worst case: 22 literal preset commands plus one phase's six-command block, its
        // four-command repair, a master question re-engaging two, and the premise check and
        // review/fix pass that are coming — about 39. 60 leaves about 21 spare where the flat
        // 100 left about 61, so a one- or two-phase run is now guarded TIGHTER than before;
        // three ties the old number and four is the first that gets more.
        var budget = StepBudget.ForPhases(1);

        budget.Limit.Should().BeLessThan(StepBudget.Default,
            "a one-phase run is deliberately guarded tighter than the flat number was");
        budget.Limit.Should().BeGreaterThan(39, "that is what one phase needs at worst");
        StepBudget.ForPhases(2).Limit.Should().BeLessThan(StepBudget.Default,
            "two phases are narrowed too");
        StepBudget.ForPhases(3).Limit.Should().Be(StepBudget.Default,
            "three phases tie the old number exactly");
        StepBudget.ForPhases(4).Limit.Should().BeGreaterThan(StepBudget.Default,
            "four is the first phase count that gets MORE than the flat ceiling");
    }

    [Fact]
    public void StepBudget_AboveTheAbsoluteCeiling_IsClamped()
    {
        var budget = StepBudget.ForPhases(1000);

        budget.Limit.Should().Be(StepBudget.AbsoluteCeiling);
        budget.Origin.Should().Contain("clamped", "the run says why its number is not the arithmetic");
    }

    [Fact]
    public async Task StepBudget_RetriggerWithFourExecutedPhases_IsSizedOnTheTail()
    {
        // The gate knows the whole set; the sequence splices only the unexecuted tail, so
        // on a re-trigger the set's own count is twice the work this run will do.
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, EightPhaseSet() with
        {
            Executed = ["p0001a", "p0001b", "p0001c", "p0001d"],
        });

        await Sequence().ExecuteAsync(new PhaseSequenceContext(pipeline), default);

        StepBudget.From(pipeline).Should().Be(StepBudget.ForPhases(4));
    }

    [Fact]
    public async Task StepBudget_NoSpecSet_IsTheDefaultHundred()
    {
        var pipeline = new PipelineContext();

        await Sequence().ExecuteAsync(new PhaseSequenceContext(pipeline), default);

        pipeline.Has(ContextKeys.StepBudget).Should().BeFalse("nothing was spliced");
        StepBudget.From(pipeline).Limit.Should().Be(100, "exactly the number the executor always had");
    }

    [Fact]
    public void StepBudget_PresetWithoutASequence_IsUnchanged()
    {
        // Everything the budget could change is reachable only through PhaseSequence, and
        // exactly one preset carries it — so every OTHER preset is untouched by construction.
        // That the executor then uses 100 is proven through the executor itself, in
        // PipelineExecutorBudgetTests.PipelineExecutor_NothingPublished_StopsAtTheHundredItAlwaysHad.
        var withASequence = PipelinePresets.Names
            .Where(name => PipelinePresets.TryResolve(name)!
                .Contains(CommandNames.PhaseSequence, StringComparer.Ordinal))
            .ToList();

        withASequence.Should().ContainSingle("one publisher, reachable from one preset")
            .Which.Should().Be(PipelinePresets.CodeName);
        PipelinePresets.Names.Should().HaveCountGreaterThan(
            1, "the premise: there are other presets for this to say something about");
    }

    [Fact]
    public async Task PhaseSpecGate_PublishesNoBudget()
    {
        // PhaseSpecGate knows every phase of the set, including the executed ones — and it
        // appears in exactly one preset, which carries PhaseSequence too. One publisher.
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, EightPhaseSet() with { Executed = ["p0001a"] });

        var result = await new PhaseSpecGateHandler(NullLogger<PhaseSpecGateHandler>.Instance)
            .ExecuteAsync(new PhaseSpecGateContext(Ticket(), pipeline), default);

        result.IsSuccess.Should().BeTrue();
        pipeline.Has(ContextKeys.StepBudget).Should().BeFalse();
    }

    private static PhaseSequenceHandler Sequence() =>
        new(NullLogger<PhaseSequenceHandler>.Instance);

    private static Ticket Ticket() =>
        new(new TicketId("42"), "A ticket", "Its description", null, "Open", "Stub");

    private static SpecSet EightPhaseSet() => new(
        "azdo-1",
        [.. new[] { "a", "b", "c", "d", "e", "f", "g", "h" }.Select(letter =>
            new SpecPhase(
                new PhaseDraft($"p0001{letter}", $"Goal of {letter}", $"phase: p0001{letter}", [])
                {
                    Done = [$"p0001{letter} is done."],
                },
                $"p0001{letter}", string.Empty, []))],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived);
}
