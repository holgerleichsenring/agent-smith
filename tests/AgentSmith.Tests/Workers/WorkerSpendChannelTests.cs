using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Infrastructure.Services.Workers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Workers;

/// <summary>
/// 2026-09-01-b0d7: worker spend has its own channel, and the flag that produces it is
/// opt-in. Both halves guard against a number that looks right and is not: a table price
/// for a call that cost no money, and a flag that kills a transport promised to accept any
/// binary reading stdin.
/// </summary>
[Collection(EnvVarCollection.Name)]
public sealed class WorkerSpendChannelTests
{
    private static readonly WorkerCallAccounting Call = new(
        // A real CLI model id, which the default table prefix-matches to claude-haiku-4-5.
        // That is the point: it WOULD price, at API rates the run never paid.
        Model: "claude-haiku-4-5-20251001",
        InputTokens: 9, OutputTokens: 113,
        CacheReadTokens: 20446, CacheCreationTokens: 15162,
        ReportedCostUsd: 0.0215711m, CliTurns: 3);

    [Fact]
    public void WorkerSpend_IsNotCountedAsPricedSpendOrAsAnUnpricedModel()
    {
        var tracker = new PipelineCostTracker(new ModelPricingResolver());

        tracker.Track(Call.AttachTo(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));

        tracker.EstimateCostUsd().Should().Be(0m,
            "a subscription-answered call spends nothing; pricing it would invent money");
        tracker.UnpricedTokensByModel.Should().BeEmpty(
            "and reporting it as unpriced would raise a pricing alarm for a call that has "
            + "no table price by design");
        tracker.WorkerCalls.ReportedCostUsd.Should().Be(0.0215711m);
        tracker.ToString().Should().NotContain("COST INCOMPLETE")
            .And.Contain("worker CLI calls");
        tracker.EffectiveBudgetTokens.Should().BeGreaterThan(0,
            "the TOKEN arm of the fence binds — context volume is real on any transport — "
            + "while the money arm has nothing to bind on");
    }

    [Fact]
    public void WorkerCall_IsNeverPricedAsASingleResponseEither()
    {
        var tracker = new PipelineCostTracker(new ModelPricingResolver());

        var cost = tracker.EstimateResponseCostUsd(
            Call.AttachTo(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));

        cost.Should().Be(0m);
    }

    [Fact]
    public void WorkerAgent_WithoutTheOptIn_GetsNoEnvelopeFlag()
    {
        using var _ = new ClearedWorkerEnvironment();

        var arguments = Arguments(new AgentConfig { Endpoint = "/opt/bin/my-own-worker" });

        arguments.Should().NotContain("--output-format",
            "worker mode promises any binary reading a prompt on stdin works, and an "
            + "unknown binary handed an unknown flag exits non-zero — a dead run");
    }

    [Fact]
    public void WorkerAgent_OnTheDefaultCli_AsksForTheStructuredResult()
    {
        using var _ = new ClearedWorkerEnvironment();

        Arguments(new AgentConfig()).Should().ContainInOrder("--output-format", "json");
        Arguments(new AgentConfig { Endpoint = "/usr/local/bin/claude" })
            .Should().ContainInOrder("--output-format", "json");
    }

    [Fact]
    public void WorkerAgent_ThatOptsOut_IsObeyedEvenOnTheDefaultCli()
    {
        using var _ = new ClearedWorkerEnvironment();

        Arguments(new AgentConfig { WorkerStructuredResult = false })
            .Should().NotContain("--output-format");
        Arguments(new AgentConfig
        {
            Endpoint = "/opt/bin/my-own-worker", WorkerStructuredResult = true,
        }).Should().Contain("--output-format");
    }

    private static IReadOnlyList<string> Arguments(AgentConfig agent) =>
        new ExternalWorkerCliOptionsFactory()
            .Create(agent, new ModelAssignment { Model = "sonnet" }).Arguments;

    /// <summary>The env escape hatches win over agent config, so they are cleared here.</summary>
    private sealed class ClearedWorkerEnvironment : IDisposable
    {
        private readonly string? _binary =
            Environment.GetEnvironmentVariable(ExternalWorkerCliOptionsFactory.BinaryEnv);
        private readonly string? _extra =
            Environment.GetEnvironmentVariable(ExternalWorkerCliOptionsFactory.ExtraArgsEnv);

        public ClearedWorkerEnvironment()
        {
            Environment.SetEnvironmentVariable(ExternalWorkerCliOptionsFactory.BinaryEnv, null);
            Environment.SetEnvironmentVariable(ExternalWorkerCliOptionsFactory.ExtraArgsEnv, null);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(ExternalWorkerCliOptionsFactory.BinaryEnv, _binary);
            Environment.SetEnvironmentVariable(ExternalWorkerCliOptionsFactory.ExtraArgsEnv, _extra);
        }
    }
}
