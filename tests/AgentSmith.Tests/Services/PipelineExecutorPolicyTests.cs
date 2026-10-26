using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

/// <summary>2026-10-26-bdd0c: a spec derivation that ended stops the run on its own sentence, which is no park.</summary>
public sealed class PipelineExecutorPolicyTests
{
    private readonly PipelineExecutorPolicy _policy = new(NullLogger<PipelineExecutorPolicy>.Instance);

    [Fact]
    public void PipelineExecutorPolicy_DerivationStopped_StopsAsSuccess()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDerivationEnded, "Spec derivation stopped: two readings");

        _policy.TryGetParkedReason(pipeline, out var message).Should().BeTrue();

        message.Should().Be("Spec derivation stopped: two readings");
        RunPark.IsWaitingForOperator(pipeline).Should().BeFalse("an ended derivation waits for nobody");
    }

    [Fact]
    public void PipelineExecutorPolicy_NothingEnded_DoesNotStop()
    {
        _policy.TryGetParkedReason(new PipelineContext(), out _).Should().BeFalse();
    }
}
