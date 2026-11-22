using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ModelQuota;

/// <summary>
/// 2026-11-22-66e4a: the budget holds what the provider counted. Measured: one run sent 250k-440k fresh and cached
/// input tokens a minute at a 250k deployment while the limiter, reserving chars/4 and discounting cached tokens to a
/// tenth, believed it was under budget and never waited.
/// </summary>
public sealed class ModelQuotaCountingTests
{
    private static readonly ModelQuotaScope Scope = new("k", "azure_openai", "dep");

    [Fact]
    public async Task Settle_AzureCall_DebitsTheCountedInputIncludingCachedAndKeepsMaxOutput()
    {
        var limiter = new LlmRateLimiter(
            new LlmRateLimitOptions(250, 250_000, LlmRateBudget.RuleFor("azure_openai")), new FrozenClock());

        var lease = await limiter.AcquireAsync(new LlmQuotaDemand(100, 8_192), CancellationToken.None);
        lease.Settle(new UsageDetails { InputTokenCount = 50_000, CachedInputTokenCount = 45_000 }, null, TimeSpan.Zero);

        limiter.Snapshot(Scope).FreeTokens.Should().Be(250_000 - 50_000 - 8_192,
            "Azure counts the cached prompt tokens and keeps the max_tokens its admission reserved");
    }

    [Fact]
    public async Task Settle_ProviderSaysLessIsLeft_CapsTheBudgetAtItsReading()
    {
        var limiter = new LlmRateLimiter(
            new LlmRateLimitOptions(250, 250_000, LlmRateBudget.RuleFor("azure_openai")), new FrozenClock());

        var lease = await limiter.AcquireAsync(new LlmQuotaDemand(100, 0), CancellationToken.None);
        lease.Settle(null, new ProviderQuotaReading(RemainingTokens: 30_000, RemainingRequests: 12), TimeSpan.Zero);

        var snapshot = limiter.Snapshot(Scope);
        snapshot.FreeTokens.Should().Be(30_000, "another caller of the deployment spent what this process cannot see");
        snapshot.FreeRequests.Should().Be(12);
        snapshot.ProviderRemainingTokens.Should().Be(30_000);
    }

    [Fact]
    public void ModelQuotaKey_TwoModelNamesOnOneAzureDeployment_ShareOneKey()
    {
        var agent = new AgentConfig { Type = "azure_openai", Endpoint = "https://x.openai.azure.com/", Deployment = "dep" };

        var primary = ModelQuotaKey.For(agent, new ModelAssignment { Model = "gpt-5" }, "azure_openai");
        var scout = ModelQuotaKey.For(agent, new ModelAssignment { Model = "gpt-5-mini" }, "azure_openai");

        scout.Key.Should().Be(primary.Key, "Azure counts its quota on the deployment the request names");
        primary.Deployment.Should().Be("dep");
    }

    [Fact]
    public void ModelQuotaKey_TwoModelsOnOpenAi_KeepTheirOwnKeys()
    {
        var agent = new AgentConfig { Type = "openai", Deployment = "stray" };

        var a = ModelQuotaKey.For(agent, new ModelAssignment { Model = "gpt-4.1" }, "openai");
        var b = ModelQuotaKey.For(agent, new ModelAssignment { Model = "gpt-4.1-mini" }, "openai");

        a.Key.Should().NotBe(b.Key, "OpenAI is sent the model name, and counts each model on its own");
    }

    [Fact]
    public void Registry_TwoRolesOfOneAzureDeployment_ShareOneLimiterAndListIt()
    {
        var registry = new LlmRateLimiterRegistry(NullLogger<LlmRateLimiterRegistry>.Instance);
        var agent = new AgentConfig { Type = "azure_openai", Endpoint = "https://x.openai.azure.com", Deployment = "dep" };
        var options = new LlmRateLimitOptions(250, 250_000);

        var master = registry.GetOrCreate(ModelQuotaKey.For(agent, new ModelAssignment { Model = "gpt-5" }, "azure_openai"), options);
        var scout = registry.GetOrCreate(ModelQuotaKey.For(agent, new ModelAssignment { Model = "gpt-5-mini" }, "azure_openai"), options);

        scout.Should().BeSameAs(master, "every caller of the deployment draws on one budget");
        registry.Read().Should().ContainSingle().Which.Deployment.Should().Be("dep");
    }

    [Fact]
    public async Task Refused_TheSameCallTwice_OwesItOnce()
    {
        var limiter = new LlmRateLimiter(
            new LlmRateLimitOptions(250, 250_000, LlmRateBudget.RuleFor("azure_openai")), new FrozenClock());
        var lease = await limiter.AcquireAsync(new LlmQuotaDemand(50_000, 0), CancellationToken.None);

        await lease.RefusedAsync(TimeSpan.Zero, CancellationToken.None);
        await lease.RefusedAsync(TimeSpan.Zero, CancellationToken.None);

        var snapshot = limiter.Snapshot(Scope);
        snapshot.FreeTokens.Should().Be(0);
        snapshot.SecondsToFull.Should().BeApproximately((250_000 + 50_000) / (250_000 / 60.0), 0.01,
            "the provider has no room and the refused call is owed once from the refill, however often it is refused");
    }

    [Fact]
    public async Task Refused_TwoSiblingsTogether_OweBothAndCountAsWaiting()
    {
        var limiter = new LlmRateLimiter(
            new LlmRateLimitOptions(250, 250_000, LlmRateBudget.RuleFor("azure_openai")), new FrozenClock());
        var a = await limiter.AcquireAsync(new LlmQuotaDemand(50_000, 0), CancellationToken.None);
        var b = await limiter.AcquireAsync(new LlmQuotaDemand(50_000, 0), CancellationToken.None);

        await a.RefusedAsync(TimeSpan.Zero, CancellationToken.None);
        await b.RefusedAsync(TimeSpan.Zero, CancellationToken.None);

        var snapshot = limiter.Snapshot(Scope);
        snapshot.SecondsToFull.Should().BeApproximately((250_000 + 100_000) / (250_000 / 60.0), 0.01,
            "both refused calls are owed from the refill, not only the larger one");
        snapshot.Waiting.Should().Be(2, "a call sitting out a refusal waits as much as one in the queue");
        a.Release();
        limiter.Snapshot(Scope).Waiting.Should().Be(1);
    }

    [Fact]
    public void Limiter_ABudgetOfZero_IsRefusedNamingTheKey()
    {
        var create = () => new LlmRateLimiter(new LlmRateLimitOptions(0, 250_000));

        create.Should().Throw<AgentSmith.Domain.Exceptions.ConfigurationException>().WithMessage("*agent.rate_limit*at least 1*");
    }

    [Fact]
    public async Task Acquire_BudgetSpent_AnnouncesTheExpectedWaitFirst()
    {
        var limiter = new LlmRateLimiter(new LlmRateLimitOptions(60, 600));
        await limiter.AcquireAsync(new LlmQuotaDemand(600, 0), CancellationToken.None);
        TimeSpan? announced = null;
        using var cancel = new CancellationTokenSource();

        var waiting = limiter.AcquireAsync(new LlmQuotaDemand(20, 0, (wait, _, _) =>
        {
            announced = wait;
            cancel.Cancel();
            return Task.CompletedTask;
        }), cancel.Token);

        await FluentActions.Awaiting(() => waiting).Should().ThrowAsync<OperationCanceledException>();
        announced.Should().BeCloseTo(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(300),
            "600 tokens a minute refill ten a second, and the call needs twenty");
    }

    /// <summary>A clock whose stopwatch never moves, so no refill blurs the arithmetic.</summary>
    private sealed class FrozenClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
