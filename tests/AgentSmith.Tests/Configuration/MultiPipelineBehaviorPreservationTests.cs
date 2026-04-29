using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// Behavior-preservation gate (p0106): the four agent-smith-* projects in
/// config/agentsmith.yml must resolve to the inline baseline values (16
/// values total) via the legacy-shim path. Any drift is a regression.
/// </summary>
public class MultiPipelineBehaviorPreservationTests
{
    private const string AgentSmithYaml = """
        projects:
          agent-smith:
            source:
              type: GitHub
              url: https://github.com/holgerleichsenring/agent-smith
              auth: token
            tickets:
              type: GitHub
              url: https://github.com/holgerleichsenring/agent-smith
              auth: token
            agent:
              type: Claude
              model: claude-sonnet-4-20250514
            pipeline: fix-bug
            coding_principles_path: .agentsmith/coding-principles.md

          agent-smith-security:
            source:
              type: GitHub
              url: https://github.com/holgerleichsenring/agent-smith
              auth: token
            tickets:
              type: GitHub
              url: https://github.com/holgerleichsenring/agent-smith
              auth: token
            agent:
              type: OpenAI
              model: llama-3.3-70b-versatile
            pipeline: security-scan
            skills_path: skills/security

          agent-smith-api-security-claude:
            source:
              type: GitHub
              url: https://github.com/holgerleichsenring/agent-smith
              auth: token
            tickets:
              type: GitHub
              url: https://github.com/holgerleichsenring/agent-smith
              auth: token
            agent:
              type: Claude
              model: claude-sonnet-4-20250514
            pipeline: api-security-scan
            skills_path: skills/api-security

          agent-smith-security-scan-azure-openai:
            source:
              type: GitHub
              url: https://github.com/holgerleichsenring/agent-smith
              auth: token
            tickets:
              type: GitHub
              url: https://github.com/holgerleichsenring/agent-smith
              auth: token
            agent:
              type: azure-openai
              endpoint: https://oai-rhegpt-dev.openai.azure.com
            pipeline: security-scan
            skills_path: skills/security
        """;

    private static AgentSmithConfig LoadBaselineConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), $"baseline-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, AgentSmithYaml);
        try
        {
            var loader = new YamlConfigurationLoader(new ProjectConfigNormalizer());
            return loader.LoadConfig(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("agent-smith", "fix-bug", "Claude", "skills/coding", ".agentsmith/coding-principles.md")]
    [InlineData("agent-smith-security", "security-scan", "OpenAI", "skills/security", null)]
    [InlineData("agent-smith-api-security-claude", "api-security-scan", "Claude", "skills/api-security", null)]
    [InlineData("agent-smith-security-scan-azure-openai", "security-scan", "azure-openai", "skills/security", null)]
    public void BehaviorPreservation_BaselineProjects_ResolveToExpectedValues(
        string projectName, string expectedPipeline, string expectedAgentType,
        string expectedSkillsPath, string? expectedCodingPrinciplesPath)
    {
        var config = LoadBaselineConfig();
        var project = config.Projects[projectName];
        var resolver = new PipelineConfigResolver();

        var pipelineName = resolver.ResolveDefaultPipelineName(project);
        var resolved = resolver.Resolve(project, pipelineName);

        pipelineName.Should().Be(expectedPipeline);
        resolved.Agent.Type.Should().Be(expectedAgentType);
        resolved.SkillsPath.Should().Be(expectedSkillsPath);
        resolved.CodingPrinciplesPath.Should().Be(expectedCodingPrinciplesPath);
    }
}
