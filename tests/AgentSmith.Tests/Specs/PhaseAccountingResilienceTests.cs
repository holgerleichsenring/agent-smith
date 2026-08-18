using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0434: taking the account is a MODEL call, and on the path where CommitAndPR reaches it
/// first, that call happens at the last step of the run — after every unit of work is done.
/// <para>
/// p0433 made this visible by declaring the PR step a model step; the consequence had been
/// true and unstated since p0420. A rate limit or a transport blip there threw the whole
/// step, so a run that had built everything lost its pull request — the p0350 shape, which
/// cost two draft PRs the one time it happened for real.
/// </para>
/// </summary>
public sealed class PhaseAccountingResilienceTests
{
    [Fact]
    public async Task AProviderFailureWhileAccounting_IsAnAccountThatCouldNotBeTaken_NotAThrow()
    {
        var sut = Build(new ThrowingAccountant("429 Too Many Requests"));

        var accounts = await sut.TakeAsync(Pipeline(), Sandboxes(), [], CancellationToken.None);

        accounts.Should().ContainSingle();
        accounts[0].Problem.Should().Contain("429 Too Many Requests");
        accounts[0].Delivered.Should().BeFalse("an account that could not be taken is not a pass");
    }

    /// <summary>
    /// The gate is unchanged by this: a run whose account could not be taken is still
    /// refused. What changes is that the refusal arrives WITH the pull request rather than
    /// instead of it.
    /// </summary>
    [Fact]
    public async Task SuchARun_IsStillRefusedByTheGate()
    {
        var sut = Build(new ThrowingAccountant("connection reset"));

        var accounts = await sut.TakeAsync(Pipeline(), Sandboxes(), [], CancellationToken.None);
        var gate = RunDeliveryGate.Evaluate(RunAccounts.Empty.With("p1", accounts), 1);

        gate.Satisfied.Should().BeFalse();
    }

    /// <summary>A cancel is not a provider blip and must still propagate.</summary>
    [Fact]
    public async Task ACancelledRun_StillCancels()
    {
        var sut = Build(new ThrowingAccountant("cancelled", asCancellation: true));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await sut.TakeAsync(Pipeline(), Sandboxes(), [], cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static PhaseAccounting Build(ISpecAccountant accountant) =>
        new(new DeliveryDiff(NullLogger<DeliveryDiff>.Instance), accountant,
            new AgentSmith.Application.Services.Handlers.SandboxTargets(),
            NullLogger<PhaseAccounting>.Instance);

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PhaseSpec,
            new PhaseDraft("p1", "goal", "phase: p1", []) { Done = ["the handler is migrated"] });
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("code", new AgentConfig(), "skills", null));
        return pipeline;
    }

    private static IReadOnlyDictionary<string, ISandbox> Sandboxes()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(
                StepResult.CurrentSchemaVersion, Guid.NewGuid(), ExitCode: 0, TimedOut: false,
                DurationSeconds: 0, ErrorMessage: null, OutputContent: "diff --git a/x b/x"));
        return new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["repo"] = sandbox.Object };
    }

    private sealed class ThrowingAccountant(string message, bool asCancellation = false) : ISpecAccountant
    {
        public Task<SpecAccount> AccountAsync(
            string repoKey, IReadOnlyList<string> criteria, string diff,
            IReadOnlyList<string> commandResults, AgentConfig agent,
            PipelineCostTracker costTracker, CancellationToken cancellationToken) =>
            asCancellation
                ? throw new OperationCanceledException(message)
                : throw new InvalidOperationException(message);
    }
}
