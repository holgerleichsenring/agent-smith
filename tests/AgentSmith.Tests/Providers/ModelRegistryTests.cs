using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers;

public class ModelRegistryTests
{
    [Fact]
    public void ModelRegistryConfig_Defaults_AreCorrect()
    {
        var config = new ModelRegistryConfig();

        config.Scout.Model.Should().Be("claude-haiku-4-5-20251001");
        config.Scout.MaxTokens.Should().Be(4096);
        config.Primary.Model.Should().Be("claude-sonnet-4-20250514");
        config.Primary.MaxTokens.Should().Be(8192);
        config.Planning.Model.Should().Be("claude-sonnet-4-20250514");
        config.Planning.MaxTokens.Should().Be(4096);
        config.Reasoning.Should().BeNull();
        config.Summarization.Model.Should().Be("claude-haiku-4-5-20251001");
        config.Summarization.MaxTokens.Should().Be(2048);
    }

    [Fact]
    public void ModelAssignment_Defaults_AreCorrect()
    {
        var assignment = new ModelAssignment();

        assignment.Model.Should().BeEmpty();
        assignment.MaxTokens.Should().Be(8192);
    }

    [Fact]
    public void ConfigBasedModelRegistry_ReturnsScoutModel()
    {
        var config = new ModelRegistryConfig();
        var registry = CreateRegistry(config);

        var result = registry.GetModel(TaskType.Scout);

        result.Model.Should().Be("claude-haiku-4-5-20251001");
        result.MaxTokens.Should().Be(4096);
    }

    [Fact]
    public void ConfigBasedModelRegistry_ReturnsPrimaryModel()
    {
        var config = new ModelRegistryConfig();
        var registry = CreateRegistry(config);

        var result = registry.GetModel(TaskType.Primary);

        result.Model.Should().Be("claude-sonnet-4-20250514");
        result.MaxTokens.Should().Be(8192);
    }

    [Fact]
    public void ConfigBasedModelRegistry_ReturnsPlanningModel()
    {
        var config = new ModelRegistryConfig();
        var registry = CreateRegistry(config);

        var result = registry.GetModel(TaskType.Planning);

        result.Model.Should().Be("claude-sonnet-4-20250514");
        result.MaxTokens.Should().Be(4096);
    }

    [Fact]
    public void ConfigBasedModelRegistry_ReasoningFallsToPrimary_WhenNotConfigured()
    {
        var config = new ModelRegistryConfig();
        var registry = CreateRegistry(config);

        var result = registry.GetModel(TaskType.Reasoning);

        result.Model.Should().Be(config.Primary.Model);
        result.MaxTokens.Should().Be(config.Primary.MaxTokens);
    }

    [Fact]
    public void ConfigBasedModelRegistry_ReasoningUsesConfigured_WhenSet()
    {
        var config = new ModelRegistryConfig
        {
            Reasoning = new ModelAssignment
            {
                Model = "claude-opus-4-20250514",
                MaxTokens = 16384
            }
        };
        var registry = CreateRegistry(config);

        var result = registry.GetModel(TaskType.Reasoning);

        result.Model.Should().Be("claude-opus-4-20250514");
        result.MaxTokens.Should().Be(16384);
    }

    [Fact]
    public void ConfigBasedModelRegistry_ReturnsSummarizationModel()
    {
        var config = new ModelRegistryConfig();
        var registry = CreateRegistry(config);

        var result = registry.GetModel(TaskType.Summarization);

        result.Model.Should().Be("claude-haiku-4-5-20251001");
        result.MaxTokens.Should().Be(2048);
    }

    [Fact]
    public void ConfigBasedModelRegistry_CustomModels_AreRespected()
    {
        var config = new ModelRegistryConfig
        {
            Scout = new ModelAssignment { Model = "custom-scout", MaxTokens = 1024 },
            Primary = new ModelAssignment { Model = "custom-primary", MaxTokens = 4096 }
        };
        var registry = CreateRegistry(config);

        registry.GetModel(TaskType.Scout).Model.Should().Be("custom-scout");
        registry.GetModel(TaskType.Primary).Model.Should().Be("custom-primary");
    }

    [Fact]
    public void AgentConfig_Models_IsNullable()
    {
        var config = new AgentConfig();

        config.Models.Should().BeNull();
    }

    [Fact]
    public void AgentConfig_Models_IsSettable()
    {
        var config = new AgentConfig
        {
            Models = new ModelRegistryConfig()
        };

        config.Models.Should().NotBeNull();
        config.Models!.Scout.Model.Should().Be("claude-haiku-4-5-20251001");
    }

    private static ConfigBasedModelRegistry CreateRegistry(ModelRegistryConfig config) =>
        new(config, NullLogger.Instance);
}
