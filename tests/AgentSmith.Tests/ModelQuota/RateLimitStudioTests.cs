using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Pricing;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ModelQuota;

/// <summary>
/// 2026-11-22-66e4b: agent.rate_limit as a studio field — carried through the studio's write and read, a number below
/// 1 refused where it is entered and at startup, and a changed budget reaching the deployment's limiter.
/// </summary>
public sealed class RateLimitStudioTests
{
    [Fact]
    public void Studio_ReadAndWrite_CarryTheBudget_AndNullRestoresTheDefault()
    {
        var stored = RawConfigPatch.Agent(Entity(new AgentRateLimitSettings(null, 250_000)), null);
        stored.RateLimit!.InputTokensPerMinute.Should().Be(250_000);
        stored.RateLimit.RequestsPerMinute.Should().BeNull("an empty number is the provider default");

        var read = ConfigCatalogMapper.ToCatalog(new RawAgentSmithConfig { Agents = { ["azure"] = stored } }).Agents.Single();
        read.RateLimit.Should().Be(new AgentRateLimitSettings(null, 250_000));

        RawConfigPatch.Agent(read with { RateLimit = null }, stored).RateLimit.Should().BeNull();
    }

    [Fact]
    public void Studio_ABudgetBelowOne_IsNeverStored()
    {
        var act = () => ConfigStudioCapabilities.ValidateAgent(Entity(new AgentRateLimitSettings(0, null)), new BundledModelPriceList());

        act.Should().Throw<ConfigurationException>().WithMessage("*Agent 'azure'*at least 1*agent.rate_limit*");
    }

    [Fact]
    public void Startup_ABudgetBelowOneOnAProjectAgent_IsABlockingFinding()
    {
        var config = new AgentSmithConfig();
        config.Projects["p"] = new ResolvedProject { Name = "p", Agent = new AgentConfig { RateLimit = new RateLimitConfig { InputTokensPerMinute = 0 } } };

        AgentConcurrencyConfigValidator.Findings(config)
            .Should().ContainSingle(f => f.Field == AgentConfig.RateLimitKey).Which.IsBlocking.Should().BeTrue();
    }

    [Fact]
    public void Budget_TwoAgentsOnOneDeployment_TakeTheLowestStatedNumber_AndTheDefaultWhereNoneStates()
    {
        var stated = Azure(new RateLimitConfig { InputTokensPerMinute = 250_000 });
        var silent = Azure(null);
        var budget = Budgets(stated, silent);
        var scope = ModelQuotaKey.For(silent, new ModelAssignment { Model = "gpt-5" }, "azure_openai");

        var options = budget.For(silent, scope, "azure_openai", () => null);

        options.InputTokensPerMinute.Should().Be(250_000, "an agent without an override never lowers another's budget");
        options.RequestsPerMinute.Should().Be(60, "no agent states requests, so the provider default applies");
    }

    [Fact]
    public void Budget_ARunStartedBeforeAnEdit_DoesNotPullTheBudgetBack()
    {
        var before = Azure(new RateLimitConfig { InputTokensPerMinute = 250_000 });
        var now = Azure(new RateLimitConfig { InputTokensPerMinute = 500_000 });
        var scope = ModelQuotaKey.For(before, new ModelAssignment { Model = "gpt-5" }, "azure_openai");

        Budgets(now).For(before, scope, "azure_openai", () => null).InputTokensPerMinute
            .Should().Be(500_000, "the configuration as it stands now decides, not the agent a running run started with");
    }

    [Fact]
    public void ModelQuotaKey_AnAgentWithoutAModel_StillHasAKey() =>
        ModelQuotaKey.For(new AgentConfig { Type = "azure_openai" }, new ModelAssignment { Model = null! }, "azure_openai")
            .Key.Should().Be("azure_openai||");

    [Fact]
    public void Shrink_ACallReservedUnderTheOldBudget_IsTakenWholeAndSettlesToTheProvidersCount()
    {
        var clock = new FrozenClock();
        var quota = new DeploymentQuota(new LlmRateLimitOptions(250, 250_000, LlmRateBudget.RuleFor("azure_openai")), clock);
        var reserved = quota.Reserve(new LlmQuotaDemand(200_000, 0));
        quota.Resize(new LlmRateLimitOptions(250, 120_000, LlmRateBudget.RuleFor("azure_openai")));

        quota.TakeOrWait(1, reserved.Total, take: true).Should().Be(TimeSpan.Zero, "it waits for no more than the bucket holds");
        new LlmQuotaLease(quota, 200_000, reserved, clock)
            .Settle(new Microsoft.Extensions.AI.UsageDetails { InputTokenCount = 200_000 }, null, TimeSpan.Zero);

        quota.Snapshot(new ModelQuotaScope("k", "azure_openai", "dep"), 0).SecondsToFull
            .Should().BeApproximately(200_000 / 2_000.0, 0.01, "the call is charged the 200k the provider counted, not the 120k it waited for");
    }

    private sealed class FrozenClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }

    [Fact]
    public async Task Registry_AChangedBudget_ReachesTheDeploymentsLimiter_AndAShrinkStrandsNoWaiter()
    {
        var registry = new LlmRateLimiterRegistry(NullLogger<LlmRateLimiterRegistry>.Instance);
        var scope = new ModelQuotaScope("k", "azure_openai", "dep");
        var limiter = registry.GetOrCreate(scope, new LlmRateLimitOptions(250, 250_000));
        await limiter.AcquireAsync(new LlmQuotaDemand(245_000, 0), CancellationToken.None);
        var waiting = limiter.AcquireAsync(new LlmQuotaDemand(240_000, 0), CancellationToken.None);

        registry.GetOrCreate(scope, new LlmRateLimitOptions(5_000, 5_000));

        registry.Read().Single().TokensPerMinute.Should().Be(5_000);
        await waiting.OrHang("the waiter reserved under the old budget to fit the new one");
    }

    private static AgentConfig Azure(RateLimitConfig? budget) =>
        new() { Type = "azure_openai", Endpoint = "https://x.openai.azure.com", Deployment = "dep", Model = "gpt-5", RateLimit = budget };

    private static DeploymentRateBudget Budgets(params AgentConfig[] agents)
    {
        var services = new ServiceCollection().AddSingleton(new ConfiguredAgents(agents)).BuildServiceProvider();
        return new DeploymentRateBudget(services);
    }

    private static AgentEntity Entity(AgentRateLimitSettings budget) =>
        new("azure", "azure_openai", null, null, null, null,
            new Dictionary<string, AgentCatalogModel> { ["m"] = new("gpt-4.1") },
            new Dictionary<string, string> { ["primary"] = "m" }, null, null, null, null, null, budget);
}
