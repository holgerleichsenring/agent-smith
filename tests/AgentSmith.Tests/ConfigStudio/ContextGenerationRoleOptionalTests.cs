using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-23-7868b: contextGeneration is an optional role like reasoning — the
/// catalog stops emitting a role the operator never stated, the patch creates the
/// assignment when one is stated, and the capabilities descriptor the form and the
/// upsert validation both read says so.
/// </summary>
public sealed class ContextGenerationRoleOptionalTests
{
    [Fact]
    public void ConfigCatalog_AgentWithoutContextGeneration_EmitsNoSuchRole()
    {
        var raw = new RawAgentSmithConfig
        {
            Agents = { ["agent"] = new AgentConfig { Model = "m", Models = new ModelRegistryConfig() } },
        };

        var models = ConfigCatalogMapper.ToCatalog(raw).Agents.Single().Models;

        models.Should().NotContainKey("contextGeneration");
        models.Should().ContainKey("primary"); // the roles that are not optional still map
    }

    [Fact]
    public void RawAgentModelPatch_ContextGenerationOnAnAgentWithoutIt_CreatesTheAssignment()
    {
        var agent = new AgentConfig { Model = "m" };

        RawAgentModelPatch.Apply(Entity(new AgentModelAssignment("gemini-2.5-flash", null, 3072)), agent);

        agent.Models!.ContextGeneration!.Model.Should().Be("gemini-2.5-flash");
        agent.Models.ContextGeneration.MaxTokens.Should().Be(3072);
    }

    [Fact]
    public void ConfigStudioCapabilities_ContextGeneration_IsOptional() =>
        ConfigStudioCapabilities.RoleCapabilities
            .Single(r => r.Key == "contextGeneration").Optional.Should().BeTrue();

    private static AgentEntity Entity(AgentModelAssignment contextGeneration) =>
        new(
            "agent", "stub", null, null, null, null,
            new Dictionary<string, AgentModelAssignment> { ["contextGeneration"] = contextGeneration },
            null, null, null, null);
}
