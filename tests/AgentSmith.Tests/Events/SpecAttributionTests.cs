using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0466: the spec is producer knowledge, exactly as the step index is. The runner
/// opens one frame per step carrying both, and what is published inside it reads the
/// spec from there — nothing downstream recovers it from a display label.
/// </summary>
public sealed class SpecAttributionTests
{
    private readonly IRunContextAccessor _runContext = new AsyncLocalRunContextAccessor();

    [Fact]
    public void StepScope_OpenedWithASpec_ExposesIt()
    {
        using (_runContext.BeginStepScope(3, "p19213a"))
        {
            _runContext.CurrentSpecId.Should().Be("p19213a");
            _runContext.CurrentStepIndex.Should().Be(3);
        }

        _runContext.CurrentSpecId.Should().BeNull("the frame unwinds with the step");
    }

    [Fact]
    public void StepScope_StepOutsideAnySpec_HasNoSpec()
    {
        using var _ = _runContext.BeginStepScope(0);

        _runContext.CurrentSpecId.Should().BeNull();
    }

    [Fact]
    public void StepScope_NestedFrames_UnwindToTheEnclosingSpec()
    {
        using var outer = _runContext.BeginStepScope(1, "p19213a");
        using (_runContext.BeginStepScope(2, "p19213b"))
            _runContext.CurrentSpecId.Should().Be("p19213b");

        _runContext.CurrentSpecId.Should().Be("p19213a");
    }

    [Fact]
    public async Task DecisionLogged_InsideASpecStep_CarriesThatSpec()
    {
        var publisher = EventTestStubs.Recording();
        using (_runContext.BeginScope("run-1"))
        using (_runContext.BeginStepScope(4, "p19213a"))
            await new DecisionEventMirror(publisher, _runContext).PublishAsync(
                DecisionCategory.Tooling, "sqlite over postgres", "smallest footprint",
                CancellationToken.None);

        publisher.Events.OfType<DecisionLoggedEvent>().Single().PhaseId.Should().Be("p19213a");
    }

    [Fact]
    public async Task DecisionLogged_OutsideAnySpec_CarriesNone()
    {
        var publisher = EventTestStubs.Recording();
        using (_runContext.BeginScope("run-1"))
            await new DecisionEventMirror(publisher, _runContext).PublishAsync(
                DecisionCategory.Tooling, "sqlite over postgres", "smallest footprint",
                CancellationToken.None);

        publisher.Events.OfType<DecisionLoggedEvent>().Single().PhaseId.Should().BeNull();
    }
}
