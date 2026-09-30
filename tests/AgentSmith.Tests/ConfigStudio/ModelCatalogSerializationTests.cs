using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-30-62bab: a tier is a string in the stored document, on the wire and in YAML, and a
/// role stored as its use exports as its use — not beside a model '' and max_tokens 8192.
/// </summary>
public sealed class ModelCatalogSerializationTests
{
    [Fact]
    public void StoredDocument_Tier_RoundTripsAsALowercaseString()
    {
        var options = new ConfigDocJson().Options;
        var json = JsonSerializer.Serialize(new CatalogModel { Model = "m", Tier = ModelTier.Strong }, options);

        json.Should().Contain("\"Tier\":\"strong\"");
        JsonSerializer.Deserialize<CatalogModel>(json, options)!.Tier.Should().Be(ModelTier.Strong);
        JsonSerializer.Deserialize<CatalogModel>("{\"tier\":\"Fast\"}", options)!.Tier.Should().Be(ModelTier.Fast);
        JsonSerializer.Deserialize<CatalogModel>("{\"model\":\"m\"}", options)!.Tier.Should().BeNull();
    }

    [Fact]
    public void Wire_Agent_CarriesCatalogAndRoleNames()
    {
        var entity = new AgentEntity("a", "openai", null, null, null, null,
            new Dictionary<string, AgentCatalogModel> { ["main"] = new("gpt-5", Tier: ModelTier.Fast) },
            new Dictionary<string, string> { ["primary"] = "main" }, null, null, null, null);

        var json = JsonSerializer.Serialize(entity, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("\"catalog\":{\"main\":{\"model\":\"gpt-5\"").And.Contain("\"tier\":\"fast\"")
            .And.Contain("\"models\":{\"primary\":\"main\"}");
        JsonSerializer.Deserialize<AgentEntity>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .Catalog["main"].Tier.Should().Be(ModelTier.Fast);
    }

    [Fact]
    public void Yaml_CatalogAgent_ExportsTierAndBareUseAndLoadsBack()
    {
        var raw = new RawAgentSmithConfig
        {
            Agents =
            {
                ["a"] = new AgentConfig
                {
                    Type = "openai",
                    Catalog = new() { ["main"] = new() { Model = "gpt-5", MaxTokens = 4096, Tier = ModelTier.Strong } },
                    Models = new ModelRegistryConfig { Primary = new() { Use = "main" } },
                },
            },
        };
        var yaml = new RawConfigYaml();

        var text = yaml.Serialize(raw);

        text.Should().Contain("tier: strong").And.Contain("use: main")
            .And.NotContain("8192").And.NotContain("model: ''").And.NotContain("model: \"\"");
        var agent = yaml.Deserialize(text).Agents["a"];
        agent.Catalog["main"].Tier.Should().Be(ModelTier.Strong);
        new ModelRoleChain(agent).For(TaskType.Primary).MaxTokens.Should().Be(4096);
    }

    [Fact]
    public void Yaml_InlineRole_StillExportsItsModelAndCap()
    {
        var raw = new RawAgentSmithConfig
        {
            Agents = { ["a"] = new AgentConfig { Type = "openai", Models = new() { Scout = new() { Model = "m", MaxTokens = 2048 } } } },
        };

        new RawConfigYaml().Serialize(raw).Should().Contain("model: m").And.Contain("max_tokens: 2048");
    }
}
