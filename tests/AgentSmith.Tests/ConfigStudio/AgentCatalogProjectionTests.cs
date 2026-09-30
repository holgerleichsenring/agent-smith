using AgentSmith.Application.Services.Pricing;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-30-62bab: a stored legacy agent opens as a catalog and saves in catalog form, and no
/// role's call changes — model, deployment, output cap, window, provider and endpoint are the
/// same before and after for every role.
/// </summary>
public sealed class AgentCatalogProjectionTests
{
    public static TheoryData<string> Fixtures => new() { "agent-only", "inline-roles", "mixed" };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void OpenAndSave_EveryRole_ResolvesToTheSameCall(string fixture)
    {
        var before = Legacy(fixture);
        var entity = Open(Legacy(fixture));

        ConfigStudioCapabilities.ValidateAgent(entity, new BundledModelPriceList());
        var saved = RawConfigPatch.Agent(entity, Legacy(fixture));

        foreach (var task in Enum.GetValues<TaskType>())
            Call(saved, task).Should().BeEquivalentTo(Call(before, task), $"role {task} must not change");
        saved.Model.Should().BeEmpty();
        saved.Deployment.Should().BeNull();
    }

    [Fact]
    public void Open_InlineRoles_NamesEntriesByModelIdSuffixedInRoleOrder()
    {
        var entity = Open(Legacy("inline-roles"));

        entity.Models.Should().Equal(new Dictionary<string, string>
        {
            ["primary"] = "gpt-4.1", ["scout"] = "gpt-4.1-mini", ["planning"] = "gpt-4.1",
            ["reasoning"] = "o3", ["summarization"] = "gpt-4.1-2", ["codeMapGeneration"] = "gpt-4.1-mini-2",
        });
        entity.Catalog["gpt-4.1"].Should().Be(new AgentCatalogModel("gpt-4.1", "d1", 16000, 128000));
        entity.Catalog["gpt-4.1-mini"].Deployment.Should().Be("d1", "the deployment it was called with");
    }

    [Fact]
    public void Open_InlineRoleMatchingADeclaredEntry_UsesThatEntryAndKeepsItsTier()
    {
        var entity = Open(Legacy("mixed"));

        entity.Models["scout"].Should().Be("main");
        entity.Catalog.Should().ContainSingle().Which.Value.Tier.Should().Be(ModelTier.Strong);
    }

    [Fact]
    public void Save_StoresEachRoleAsItsUseOnly()
    {
        var saved = RawConfigPatch.Agent(Open(Legacy("inline-roles")), Legacy("inline-roles"));

        saved.Models!.Scout.Should().BeEquivalentTo(new ModelAssignment { Use = "gpt-4.1-mini" });
        saved.Models.ContextGeneration.Should().BeNull("an unset role stays unset and inherits");
    }

    private static AgentEntity Open(AgentConfig agent) =>
        ConfigCatalogMapper.ToCatalog(new RawAgentSmithConfig { Agents = { ["a"] = agent } }).Agents.Single();

    /// <summary>What a call is made with: the chain's assignment plus the agent-level fallbacks
    /// the builders apply (deployment, endpoint).</summary>
    private static object Call(AgentConfig agent, TaskType task)
    {
        var a = new ModelRoleChain(agent).For(task);
        return new
        {
            a.Model, Deployment = a.Deployment ?? agent.Deployment ?? a.Model, a.MaxTokens,
            a.ContextWindowTokens, Provider = a.ProviderType ?? agent.Type, Endpoint = a.EffectiveEndpoint(agent),
        };
    }

    private static AgentConfig Legacy(string fixture) => fixture switch
    {
        "agent-only" => new() { Type = "azure_openai", Model = "gpt-4.1", Deployment = "d1", Endpoint = "https://e" },
        "inline-roles" => new()
        {
            Type = "azure_openai", Model = "gpt-4.1", Deployment = "d1", Endpoint = "https://e",
            Models = new ModelRegistryConfig
            {
                Primary = new() { MaxTokens = 16000, ContextWindowTokens = 128000 },
                Scout = new() { Model = "gpt-4.1-mini" },
                Planning = new() { Model = "gpt-4.1", Deployment = "d1", MaxTokens = 16000, ContextWindowTokens = 128000 },
                Reasoning = new() { Model = "o3", ProviderType = "openai", Endpoint = "https://o" },
                Summarization = new() { Model = "gpt-4.1", MaxTokens = 2048 },
                ContextGeneration = new() { Model = "" },
                CodeMapGeneration = new() { Model = "gpt-4.1-mini", Deployment = "d-mini", MaxTokens = 4096 },
            },
        },
        _ => new()
        {
            Type = "openai", Model = "ignored-beside-primary",
            Catalog = new() { ["main"] = new() { Model = "gpt-5", Tier = ModelTier.Strong } },
            Models = new ModelRegistryConfig { Primary = new() { Use = "main" }, Scout = new() { Model = "gpt-5" } },
        },
    };
}
