using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using AgentSmith.Application.Services.Events;

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
        return new YamlConfigurationLoader(new RawConfigMaterializer(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(), new ConfigCatalogResolver(), new AgentSmithPaths()), new NoOpSystemEventPublisher())
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
              max_seconds_per_skill_call: 240
              max_concurrent_skill_calls: 4
            secrets: {}
            """);

        cfg.Limits.MaxToolCallsPerSkill.Should().Be(42);
        cfg.Limits.MaxToolCallsPerInvestigator.Should().Be(7);
        cfg.Limits.MaxToolCallsPerVerifier.Should().Be(19);
        cfg.Limits.MaxLlmCallsPerSkill.Should().Be(12);
        cfg.Limits.MaxInputTokensPerSkillCall.Should().Be(123456);
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
        cfg.Limits.MaxInputTokensPerSkillCall.Should().Be(500_000); // p0236: raised 200k → 500k
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

    // 2026-11-12-cd4cl: a design turn's budgets are read off the agent, beside its compaction.
    [Fact]
    public void BindFromYaml_AgentDialogBudgets_BindAndDefault()
    {
        var cfg = Load("""
            agents:
              tuned:
                type: Claude
                model: claude-sonnet-4-20250514
                dialog_master_loop_iterations: 300
                dialog_sub_agent_loop_iterations: 50
                dialog_review_look_allowance: 40
                dialog_transcript: { verbatim_chars: 90000, keep_recent_chars: 45000, min_recent_turns: 3, max_fold_chars: 100000 }
              plain: { type: Claude, model: claude-sonnet-4-20250514 }
            projects: {}
            secrets: {}
            """);

        var tuned = cfg.Agents["tuned"];
        (tuned.DialogMasterLoopIterations, tuned.DialogSubAgentLoopIterations, tuned.DialogReviewLookAllowance)
            .Should().Be((300, 50, 40));
        (tuned.DialogTranscript.VerbatimChars, tuned.DialogTranscript.KeepRecentChars, tuned.DialogTranscript.MinRecentTurns,
            tuned.DialogTranscript.MaxFoldChars).Should().Be((90_000, 45_000, 3, 100_000));
        var plain = cfg.Agents["plain"];
        (plain.DialogMasterLoopIterations, plain.DialogSubAgentLoopIterations, plain.DialogReviewLookAllowance)
            .Should().Be((200, 100, 24), "the run master's and run child's defaults, and the attack reviewer's floor");
        plain.DialogTranscript.VerbatimChars.Should().Be(60_000);
    }
}
