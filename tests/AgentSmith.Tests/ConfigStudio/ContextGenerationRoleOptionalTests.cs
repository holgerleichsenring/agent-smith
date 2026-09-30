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
/// upsert validation both read says so. 2026-09-30-62bab: primary is the one required role.
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
    public void RawAgentModelPatch_RoleSavedWithoutAnEntry_IsUnset()
    {
        var agent = new AgentConfig
        {
            Model = "m", Models = new ModelRegistryConfig { Scout = new() { Model = "claude-haiku-4-5-20251001" } },
        };

        RawAgentModelPatch.Apply(Entity("scout", ""), agent);

        agent.Models!.Scout.Should().BeNull("an empty entry name is how the studio clears a role");
    }

    [Fact]
    public void RawAgentModelPatch_ContextGenerationOnAnAgentWithoutIt_CreatesTheAssignment()
    {
        var agent = new AgentConfig { Model = "m" };

        RawAgentModelPatch.Apply(Entity("contextGeneration", "flash"), agent);

        agent.Models!.ContextGeneration!.Use.Should().Be("flash");
    }

    [Fact]
    public void ConfigStudioCapabilities_EveryRoleButPrimary_IsOptional() =>
        ConfigStudioCapabilities.RoleCapabilities
            .Should().OnlyContain(r => r.Optional == (r.Key != "primary"));

    private static AgentEntity Entity(string role, string use) =>
        new(
            "agent", "stub", null, null, null, null,
            new Dictionary<string, AgentCatalogModel>
            {
                ["main"] = new("gpt-5"), ["flash"] = new("gemini-2.5-flash", MaxTokens: 3072),
            },
            new Dictionary<string, string> { ["primary"] = "main", [role] = use },
            null, null, null, null);
}
