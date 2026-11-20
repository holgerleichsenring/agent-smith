using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Pricing;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ModelSlots;

/// <summary>
/// 2026-11-20-24bd: agent.max_concurrent_runs as configuration — refused below 1 at startup and in the studio with a
/// message naming the value, the reason and the key; carried through the studio's read and write unchanged; null
/// lifts the limit.
/// </summary>
public sealed class AgentConcurrencyConfigTests
{
    [Fact]
    public void Startup_MaxConcurrentRunsZero_IsABlockingFindingNamingTheKey()
    {
        var config = new AgentSmithConfig { Agents = { ["azure"] = new AgentConfig { Type = "azure_openai", MaxConcurrentRuns = 0 } } };

        var finding = new AgentSmithConfigValidator(new SkillsConfigValidator()).Findings(config)
            .Should().ContainSingle(f => f.Field == AgentConfig.MaxConcurrentRunsKey).Subject;

        finding.IsBlocking.Should().BeTrue();
        finding.Reason.Should().Contain("Agent 'azure'").And.Contain("max_concurrent_runs is 0")
            .And.Contain("at least 1").And.Contain("agent.max_concurrent_runs");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(4)]
    public void Startup_UnsetOrPositive_IsNoFinding(int? limit) =>
        AgentConcurrencyConfigValidator.Findings(new AgentSmithConfig
        {
            Agents = { ["azure"] = new AgentConfig { MaxConcurrentRuns = limit } },
        }).Should().BeEmpty();

    [Fact]
    public void Studio_MaxConcurrentRunsZero_IsNeverStored()
    {
        var act = () => ConfigStudioCapabilities.ValidateAgent(Entity(0), new BundledModelPriceList());

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Agent 'azure'*max_concurrent_runs is 0*agent.max_concurrent_runs*");
    }

    [Fact]
    public void Studio_ReadAndWrite_CarryTheLimit_AndNullLiftsIt()
    {
        var stored = RawConfigPatch.Agent(Entity(2), null);
        stored.MaxConcurrentRuns.Should().Be(2);

        var read = ConfigCatalogMapper.ToCatalog(new RawAgentSmithConfig { Agents = { ["azure"] = stored } }).Agents.Single();
        read.MaxConcurrentRuns.Should().Be(2);

        RawConfigPatch.Agent(read with { MaxConcurrentRuns = null }, stored).MaxConcurrentRuns
            .Should().BeNull("clearing the field in the form lifts the limit");
    }

    private static AgentEntity Entity(int? limit) =>
        new("azure", "azure_openai", null, null, null, null,
            new Dictionary<string, AgentCatalogModel> { ["m"] = new("gpt-4.1") },
            new Dictionary<string, string> { ["primary"] = "m" }, null, null, null, null, limit);
}
