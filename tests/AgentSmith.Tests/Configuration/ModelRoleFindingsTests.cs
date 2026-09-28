using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// The role chain changes what an unset role answers on, and a stored configuration still
/// carries the built-in Claude ids it materialised — both are said at load, neither refused.
/// </summary>
public sealed class ModelRoleFindingsTests
{
    private readonly ModelRoleFindings _findings = new();

    [Fact]
    public void For_PartialModelsBlock_NamesEveryInheritingRole()
    {
        var agents = Agents(new AgentConfig
        {
            Type = "claude", Model = "claude-sonnet-4-5",
            Models = new ModelRegistryConfig { Primary = new() { Model = "claude-sonnet-4-5" } },
        });

        var finding = _findings.For(agents).Should().ContainSingle().Subject;

        finding.Severity.Should().Be(StartupFindingSeverity.Advisory);
        finding.Reason.Should().Contain("scout, planning, summarization").And.Contain("claude-sonnet-4-5");
    }

    [Fact]
    public void For_NoModelsBlock_SaysNothing() =>
        _findings.For(Agents(new AgentConfig { Type = "openai", Model = "gpt-5" })).Should().BeEmpty();

    [Fact]
    public void For_NonClaudeAgentCarryingStoredClaudeDefaults_NamesThoseRoles()
    {
        var agents = Agents(new AgentConfig
        {
            Type = "openai", Model = "gpt-5",
            Models = new ModelRegistryConfig
            {
                Primary = new() { Model = "gpt-5" },
                Scout = new() { Model = "claude-haiku-4-5-20251001" },
                Planning = new() { Model = "claude-sonnet-4-20250514" },
                Summarization = new() { Model = "claude-haiku-4-5-20251001" },
            },
        });

        var finding = _findings.For(agents).Should().ContainSingle().Subject;

        finding.Reason.Should().Contain("scout, planning, summarization").And.Contain("'openai'");
        finding.Field.Should().Be("agents.a.models");
    }

    [Fact]
    public void For_ClaudeAgentOrClaudeRoleProvider_IsNotReported()
    {
        var claudeRoles = new ModelRegistryConfig
        {
            Primary = new() { Model = "claude-sonnet-4-20250514" },
            Scout = new() { Model = "claude-haiku-4-5-20251001" },
            Planning = new() { Model = "claude-sonnet-4-20250514" },
            Summarization = new() { Model = "claude-haiku-4-5-20251001", ProviderType = "claude" },
        };

        _findings.For(Agents(new AgentConfig { Type = "claude", Models = claudeRoles })).Should().BeEmpty();
    }

    private static Dictionary<string, AgentConfig> Agents(AgentConfig agent) => new() { ["a"] = agent };
}
