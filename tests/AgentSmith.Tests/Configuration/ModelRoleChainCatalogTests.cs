using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-09-30-62bab: a role names a catalog entry with use:, the chain resolves it there, and
/// use wins over any inline field beside it.
/// </summary>
public sealed class ModelRoleChainCatalogTests
{
    private static AgentConfig Agent(ModelRegistryConfig models) => new()
    {
        Type = "openai",
        Catalog = new Dictionary<string, CatalogModel>
        {
            ["big"] = new()
            {
                Model = "gpt-5.6", Deployment = "d-big", MaxTokens = 16000, ContextWindowTokens = 400000,
                ProviderType = "azure_openai", Endpoint = "https://big.example", Tier = ModelTier.Strong,
            },
            ["mini"] = new() { Model = "gpt-5.6-mini", MaxTokens = 4096, Tier = ModelTier.Fast },
        },
        Models = models,
    };

    [Fact]
    public void For_UseNamesAnEntry_ResolvesEveryFieldFromIt()
    {
        var primary = new ModelRoleChain(Agent(new() { Primary = new() { Use = "big" } })).For(TaskType.Primary);

        primary.Should().BeEquivalentTo(new ModelAssignment
        {
            Model = "gpt-5.6", Deployment = "d-big", MaxTokens = 16000, ContextWindowTokens = 400000,
            ProviderType = "azure_openai", Endpoint = "https://big.example",
        });
    }

    [Fact]
    public void For_UseBesideInlineFields_UseWins()
    {
        var agent = Agent(new() { Primary = new() { Use = "mini", Model = "other", MaxTokens = 1 } });

        var primary = new ModelRoleChain(agent).For(TaskType.Primary);

        primary.Model.Should().Be("gpt-5.6-mini");
        primary.MaxTokens.Should().Be(4096);
    }

    [Fact]
    public void For_CodeMapUnset_FollowsScoutThenPrimaryThroughUse()
    {
        var withScout = new ModelRoleChain(Agent(new() { Primary = new() { Use = "big" }, Scout = new() { Use = "mini" } }));
        var withoutScout = new ModelRoleChain(Agent(new() { Primary = new() { Use = "big" } }));

        withScout.For(TaskType.CodeMapGeneration).Model.Should().Be("gpt-5.6-mini");
        withoutScout.For(TaskType.CodeMapGeneration).Model.Should().Be("gpt-5.6");
        withoutScout.EntryFor(TaskType.Planning).Should().Be("big");
    }

    [Fact]
    public void InheritingFormerDefaults_CountsAUseRoleAsSet()
    {
        var agent = Agent(new()
        {
            Primary = new() { Use = "big" }, Scout = new() { Use = "mini" }, Planning = new() { Use = "big" },
        });

        new ModelRoleChain(agent).InheritingFormerDefaults().Should().Equal("summarization");
    }

    [Fact]
    public void For_UseNamingAnUndeclaredEntry_ThrowsNamingTheRole()
    {
        var chain = new ModelRoleChain(Agent(new() { Primary = new() { Use = "big" }, Scout = new() { Use = "gone" } }));

        var act = () => chain.For(TaskType.Scout);

        act.Should().Throw<ConfigurationException>().WithMessage("*role 'scout'*'gone'*");
        chain.TryFor(TaskType.Scout).Should().BeNull();
        chain.UnknownUses().Should().Equal(("scout", "gone"));
        chain.For(TaskType.Planning).Model.Should().Be("gpt-5.6", "a role that never reaches scout is unaffected");
    }

    [Fact]
    public void PrimaryModel_ClearedAgentModel_ComesFromTheCatalog() =>
        new ModelRoleChain(Agent(new() { Primary = new() { Use = "big" } })).PrimaryModel.Should().Be("gpt-5.6");

    [Fact]
    public void EntryFor_InlineRole_IsNull() =>
        new ModelRoleChain(Agent(new() { Primary = new() { Model = "x" } })).EntryFor(TaskType.Primary).Should().BeNull();
}
