using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

public sealed class LoopLimitsConfigBindingTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(),
        $"agentsmith-limits-yaml-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private AgentSmithConfig Load(string yaml)
    {
        File.WriteAllText(_tempFile, yaml);
        return new YamlConfigurationLoader(new ProjectConfigNormalizer(), new AgentSmithPaths())
            .LoadConfig(_tempFile);
    }

    [Fact]
    public void BindFromYaml_AllFieldsPresent_BindsCorrectly()
    {
        var cfg = Load("""
            projects: {}
            limits:
              max_tool_calls_per_skill: 42
              max_tool_calls_per_investigator: 7
              max_tool_calls_per_verifier: 19
              max_llm_calls_per_skill: 12
              max_input_tokens_per_skill_call: 123456
              max_output_tokens_per_skill_call: 9000
              max_seconds_per_skill_call: 240
              max_concurrent_skill_calls: 4
            secrets: {}
            """);

        cfg.Limits.MaxToolCallsPerSkill.Should().Be(42);
        cfg.Limits.MaxToolCallsPerInvestigator.Should().Be(7);
        cfg.Limits.MaxToolCallsPerVerifier.Should().Be(19);
        cfg.Limits.MaxLlmCallsPerSkill.Should().Be(12);
        cfg.Limits.MaxInputTokensPerSkillCall.Should().Be(123456);
        cfg.Limits.MaxOutputTokensPerSkillCall.Should().Be(9000);
        cfg.Limits.MaxSecondsPerSkillCall.Should().Be(240);
        cfg.Limits.MaxConcurrentSkillCalls.Should().Be(4);
    }

    [Fact]
    public void BindFromYaml_MissingFields_UsesDefaults()
    {
        var cfg = Load("""
            projects: {}
            secrets: {}
            """);

        cfg.Limits.MaxToolCallsPerSkill.Should().Be(30);
        cfg.Limits.MaxToolCallsPerInvestigator.Should().Be(10);
        cfg.Limits.MaxToolCallsPerVerifier.Should().Be(20);
        cfg.Limits.MaxLlmCallsPerSkill.Should().Be(15);
        cfg.Limits.MaxInputTokensPerSkillCall.Should().Be(200_000);
        cfg.Limits.MaxOutputTokensPerSkillCall.Should().Be(16_000);
        cfg.Limits.MaxSecondsPerSkillCall.Should().Be(300);
        cfg.Limits.MaxConcurrentSkillCalls.Should().Be(10);
    }

    [Fact]
    public void ResolveToolCallCap_VerifyDiffMode_ReturnsVerifierCap()
    {
        var limits = new LoopLimitsConfig();
        limits.ResolveToolCallCap("verify_diff").Should().Be(limits.MaxToolCallsPerVerifier);
    }

    [Fact]
    public void ResolveToolCallCap_VerifyHintMode_ReturnsInvestigatorCap()
    {
        var limits = new LoopLimitsConfig();
        limits.ResolveToolCallCap("verify_hint").Should().Be(limits.MaxToolCallsPerInvestigator);
    }

    [Fact]
    public void ResolveToolCallCap_SurveyMode_ReturnsInvestigatorCap()
    {
        var limits = new LoopLimitsConfig();
        limits.ResolveToolCallCap("survey").Should().Be(limits.MaxToolCallsPerInvestigator);
    }

    [Fact]
    public void ResolveToolCallCap_NullMode_ReturnsSkillCap()
    {
        var limits = new LoopLimitsConfig();
        limits.ResolveToolCallCap(null).Should().Be(limits.MaxToolCallsPerSkill);
    }

    [Fact]
    public void ResolveToolCallCap_UnknownMode_FallsBackToSkillCap()
    {
        var limits = new LoopLimitsConfig();
        limits.ResolveToolCallCap("not-a-real-mode").Should().Be(limits.MaxToolCallsPerSkill);
    }
}
