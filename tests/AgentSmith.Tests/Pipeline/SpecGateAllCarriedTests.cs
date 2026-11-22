using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>
/// 2026-11-22-a720a: an attempt over a set every spec of which an earlier attempt executed (measured: a series attempt
/// started after a Retry unchanged finished the series) has nothing to build. It made the first carried spec current,
/// so its criteria counted as ratified, no spec reported, and the one delivery gate failed the run.
/// </summary>
public sealed class SpecGateAllCarriedTests
{
    private static readonly string[] Ids = ["2026-11-22-297aa", "2026-11-22-297ab", "2026-11-22-297ac"];

    [Fact]
    public async Task SpecGate_EverySpecCarried_MakesNoSpecCurrent_AndTheRunIsNotFailed()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, Set() with { Executed = Ids });

        var result = await Gate(pipeline);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("nothing to build");
        pipeline.Has(ContextKeys.CurrentSpec).Should().BeFalse();
        AcceptanceCriteria.For(pipeline).Should().BeEmpty("no carried spec's criteria are this run's to account for");
        RunDeliveryGate.Mechanical(RunAccountLedger.Current(pipeline), AcceptanceCriteria.For(pipeline).Count)
            .Satisfied.Should().BeTrue("a run with nothing to build is not a failed delivery");
    }

    [Fact]
    public async Task SpecGate_SomeSpecsLeft_StillMakesTheFirstSpecCurrent()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecSet, Set() with { Executed = Ids[..2] });

        var result = await Gate(pipeline);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec).SpecId.Should().Be(Ids[0]);
    }

    private static Task<CommandResult> Gate(PipelineContext pipeline) =>
        new SpecGateHandler(NullLogger<SpecGateHandler>.Instance).ExecuteAsync(
            new SpecGateContext(new Ticket(new TicketId("42"), "A ticket", "Its description", null, "Open", "Stub"), pipeline),
            CancellationToken.None);

    private static SpecSet Set() => new(
        "github:42",
        [.. Ids.Select(id => new DerivedSpec(new SpecDraft(id, $"Goal of {id}", $"spec: {id}", []) { Done = [$"{id} is done."] },
            id, string.Empty, []))],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Approved);
}
