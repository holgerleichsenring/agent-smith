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
            Agents =
            {
                ["agent"] = new AgentConfig
                {
                    Model = "m", Models = new ModelRegistryConfig { Primary = new() { Model = "p" } },
                },
            },
        };

        var models = ConfigCatalogMapper.ToCatalog(raw).Agents.Single().Models;

        models.Should().NotContainKey("contextGeneration");
        models.Should().NotContainKey("scout", "an unset role inherits and is not shown as a value");
        models.Should().ContainKey("primary"); // a role the operator set still maps
    }

    [Fact]
    public void RawAgentModelPatch_RoleSavedWithAnEmptyModel_IsUnset()
    {
        var agent = new AgentConfig
        {
            Model = "m", Models = new ModelRegistryConfig { Scout = new() { Model = "claude-haiku-4-5-20251001" } },
        };

        RawAgentModelPatch.Apply(Entity("scout", new AgentModelAssignment("", null)), agent);

        agent.Models!.Scout.Should().BeNull("an empty model is how the studio clears a role");
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
    public void ConfigStudioCapabilities_EveryRoleButCoding_IsOptional() =>
        ConfigStudioCapabilities.RoleCapabilities
            .Should().OnlyContain(r => r.Optional == (r.Key != ConfigStudioCapabilities.ReservedCodingRole));

    private static AgentEntity Entity(AgentModelAssignment contextGeneration) =>
        Entity("contextGeneration", contextGeneration);

    private static AgentEntity Entity(string role, AgentModelAssignment assignment) =>
        new(
            "agent", "stub", null, null, null, null,
            new Dictionary<string, AgentModelAssignment> { [role] = assignment },
            null, null, null, null);
}
