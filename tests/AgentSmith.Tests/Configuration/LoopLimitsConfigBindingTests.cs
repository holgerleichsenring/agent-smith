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
              max_seconds_per_skill_call: 240
              max_concurrent_sub_agents: 3
              max_sub_agents_per_run: 9
              max_sub_agents_per_dialog_turn: 2
            secrets: {}
            """);

        (cfg.Limits.MaxToolCallsPerSkill, cfg.Limits.MaxSecondsPerSkillCall, cfg.Limits.MaxConcurrentSubAgents,
            cfg.Limits.MaxSubAgentsPerRun, cfg.Limits.MaxSubAgentsPerDialogTurn).Should().Be((42, 240, 3, 9, 2));
    }

    [Fact]
    public void BindFromYaml_MissingFields_UsesDefaults()
    {
        var cfg = Load("""
            projects: {}
            secrets: {}
            """);

        (cfg.Limits.MaxToolCallsPerSkill, cfg.Limits.MaxSecondsPerSkillCall, cfg.Limits.MaxConcurrentSubAgents,
            cfg.Limits.MaxSubAgentsPerRun, cfg.Limits.MaxSubAgentsPerDialogTurn).Should().Be((30, 300, 4, 40, 8));
    }

    // 2026-11-17-274cb: a retired key in an old file still loads; the loader ignores it.
    [Fact]
    public void BindFromYaml_RetiredKeys_StillLoad()
    {
        var cfg = Load("""
            projects: {}
            limits:
              max_tool_calls_per_skill: 12
              max_llm_calls_per_skill: 15
              max_input_tokens_per_skill_call: 200000
              max_skills_per_phase: 5
            secrets: {}
            """);

        cfg.Limits.MaxToolCallsPerSkill.Should().Be(12);
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
