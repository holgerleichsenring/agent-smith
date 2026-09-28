using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers;

/// <summary>
/// The role chain: no role carries a built-in model; an unset role inherits — primary from the
/// agent's own model, the others from primary, code-map generation from scout then primary.
/// </summary>
public class ModelRegistryTests
{
    private static readonly TaskType[] AllTasks = Enum.GetValues<TaskType>();

    [Fact]
    public void ModelRegistryConfig_Defaults_NameNoModel()
    {
        var config = new ModelRegistryConfig();

        new[] { config.Scout, config.Primary, config.Planning, config.Reasoning, config.Summarization,
                config.ContextGeneration, config.CodeMapGeneration }
            .Should().AllSatisfy(role => role.Should().BeNull());
    }

    [Fact]
    public void ModelAssignment_Defaults_AreCorrect()
    {
        var assignment = new ModelAssignment();

        assignment.Model.Should().BeEmpty();
        assignment.MaxTokens.Should().Be(8192);
    }

    [Fact]
    public void PartialModels_OpenAiAgent_NoRoleResolvesToAClaudeId()
    {
        var agent = new AgentConfig
        {
            Type = "openai", Model = "gpt-5",
            Models = new ModelRegistryConfig { Planning = new() { Model = "gpt-5-mini", MaxTokens = 4096 } },
        };
        var registry = CreateRegistry(agent);

        AllTasks.Select(task => registry.GetModel(task).Model)
            .Should().AllSatisfy(model => model.Should().NotContain("claude"));
        registry.GetModel(TaskType.Planning).Model.Should().Be("gpt-5-mini");
        registry.GetModel(TaskType.Scout).Model.Should().Be("gpt-5");
    }

    [Fact]
    public void ModelsWithoutPrimary_PrimaryIsAgentModel()
    {
        var agent = new AgentConfig
        {
            Type = "azure_openai", Model = "gpt-4.1", Deployment = "gpt41",
            Models = new ModelRegistryConfig { Scout = new() { Model = "gpt-4.1-mini" } },
        };

        var primary = CreateRegistry(agent).GetModel(TaskType.Primary);

        primary.Model.Should().Be("gpt-4.1");
        primary.Deployment.Should().Be("gpt41");
    }

    [Fact]
    public void CodeMap_Unset_FollowsScoutThenPrimary()
    {
        var withScout = new AgentConfig
        {
            Model = "big",
            Models = new ModelRegistryConfig { Scout = new() { Model = "small", MaxTokens = 1024 } },
        };
        var withoutScout = new AgentConfig { Model = "big", Models = new ModelRegistryConfig() };

        CreateRegistry(withScout).GetModel(TaskType.CodeMapGeneration).Model.Should().Be("small");
        CreateRegistry(withoutScout).GetModel(TaskType.CodeMapGeneration).Model.Should().Be("big");
    }

    [Fact]
    public void InheritingRole_TakesPrimarysMaxTokens()
    {
        var agent = new AgentConfig
        {
            Model = "m",
            Models = new ModelRegistryConfig { Primary = new() { Model = "p", MaxTokens = 12000 } },
        };

        var registry = CreateRegistry(agent);

        registry.GetModel(TaskType.Summarization).Should().BeSameAs(registry.GetModel(TaskType.Primary));
        registry.GetModel(TaskType.Summarization).MaxTokens.Should().Be(12000);
    }

    [Fact]
    public void RoleWithBlankModel_CountsAsUnset()
    {
        var agent = new AgentConfig
        {
            Model = "m",
            Models = new ModelRegistryConfig { Scout = new() { Model = " ", MaxTokens = 10 } },
        };

        CreateRegistry(agent).GetModel(TaskType.Scout).Model.Should().Be("m");
    }

    [Fact]
    public void ConfigBasedModelRegistry_ReasoningUsesConfigured_WhenSet()
    {
        var agent = new AgentConfig
        {
            Model = "m",
            Models = new ModelRegistryConfig { Reasoning = new() { Model = "claude-opus-4-20250514", MaxTokens = 16384 } },
        };

        var result = CreateRegistry(agent).GetModel(TaskType.Reasoning);

        result.Model.Should().Be("claude-opus-4-20250514");
        result.MaxTokens.Should().Be(16384);
    }

    [Fact]
    public void ConfigBasedModelRegistry_NoModelsBlock_EveryRoleIsTheAgentModel()
    {
        var agent = new AgentConfig { Model = "only", Deployment = "d" };
        var registry = CreateRegistry(agent);

        AllTasks.Select(task => registry.GetModel(task).Model).Should().AllBe("only");
    }

    [Fact]
    public void InheritingFormerDefaults_NamesTheUnsetFormerlyDefaultedRoles()
    {
        var agent = new AgentConfig
        {
            Model = "m",
            Models = new ModelRegistryConfig { Primary = new() { Model = "p" }, Scout = new() { Model = "s" } },
        };

        new ModelRoleChain(agent).InheritingFormerDefaults().Should().Equal("planning", "summarization");
        new ModelRoleChain(new AgentConfig { Model = "m" }).InheritingFormerDefaults().Should().BeEmpty();
    }

    [Fact]
    public void AgentConfig_Models_IsNullable() => new AgentConfig().Models.Should().BeNull();

    private static ConfigBasedModelRegistry CreateRegistry(AgentConfig agent) =>
        new(agent, NullLogger.Instance);
}
