using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.ModelSlots;

/// <summary>2026-11-20-24bd: one key per deployment — provider type, effective endpoint and deployment of primary.</summary>
public sealed class ModelRunSlotKeyTests
{
    [Fact]
    public void For_TwoAgentsOnOneDeployment_ShareTheKey() =>
        ModelRunSlotKey.For(Azure("gpt41", "https://shared.openai.azure.com/"))
            .Should().Be(ModelRunSlotKey.For(Azure("gpt41", "https://SHARED.openai.azure.com")));

    [Fact]
    public void For_AnotherDeploymentOrEndpoint_IsAnotherKey()
    {
        var key = ModelRunSlotKey.For(Azure("gpt41", "https://shared.openai.azure.com"));

        ModelRunSlotKey.For(Azure("gpt41-mini", "https://shared.openai.azure.com")).Should().NotBe(key);
        ModelRunSlotKey.For(Azure("gpt41", "https://other.openai.azure.com")).Should().NotBe(key);
    }

    [Fact]
    public void For_NoDeployment_KeysOnTheModel_AndModelNamesIt()
    {
        var agent = new AgentConfig { Type = "anthropic", Model = "claude-opus-4-1" };

        ModelRunSlotKey.For(agent).Should().Be("anthropic||claude-opus-4-1");
        ModelRunSlotKey.Model(agent).Should().Be("claude-opus-4-1");
    }

    [Fact]
    public void For_AnAgentDeploymentUnderAPrimaryWithoutOne_KeysOnWhatTheBuilderSends()
    {
        var agent = new AgentConfig
        {
            Type = "azure_openai", Model = "gpt-4.1", Deployment = "shared-gpt41", Endpoint = "https://shared.openai.azure.com",
            Models = new ModelRegistryConfig { Primary = new ModelAssignment { Model = "gpt-4.1" } },
        };
        var primary = new ModelRoleChain(agent).For(AgentSmith.Contracts.Providers.TaskType.Primary);

        primary.EffectiveDeployment(agent).Should().Be("shared-gpt41", "the builder sends assignment ?? agent deployment ?? model");
        ModelRunSlotKey.For(agent).Should().EndWith("|shared-gpt41");
        ModelRunSlotKey.Model(agent).Should().Be("shared-gpt41");
    }

    [Fact]
    public void ConfiguredAgents_LimitFor_TheLowestOnTheKey_NullWhenLifted_FallbackWhenUnknown()
    {
        var key = ModelRunSlotKey.For(Azure("gpt41", "https://shared.openai.azure.com"));
        AgentConfig With(int? limit) { var a = Azure("gpt41", "https://shared.openai.azure.com"); a.MaxConcurrentRuns = limit; return a; }

        new ConfiguredAgents([With(3), With(1)]).LimitFor(key, 5).Should().Be(1);
        new ConfiguredAgents([With(null)]).LimitFor(key, 5).Should().BeNull();
        new ConfiguredAgents([]).LimitFor(key, 5).Should().Be(5);
        var config = new AgentSmithConfig { Projects = { ["p"] = new ResolvedProject { Agent = With(2) } } };
        ConfiguredAgents.Of(config).LimitFor(key, 5).Should().Be(2, "a project's own agent is configuration too");
    }

    [Fact]
    public void For_AKeyLongerThanTheColumn_FitsAndStaysDistinct()
    {
        var first = ModelRunSlotKey.For(Azure("d", "https://" + new string('a', 300)));
        var second = ModelRunSlotKey.For(Azure("e", "https://" + new string('a', 300)));

        first.Length.Should().BeLessThanOrEqualTo(ModelRunSlotKey.MaxLength);
        first.Should().NotBe(second);
    }

    private static AgentConfig Azure(string deployment, string endpoint) => new()
    {
        Type = "azure_openai", Model = "gpt-4.1", Deployment = deployment, Endpoint = endpoint,
    };
}
